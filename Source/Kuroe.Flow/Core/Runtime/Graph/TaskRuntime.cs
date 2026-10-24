using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Nodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Graph;

/// <summary>按图驱动的执行推进：激活、发布、返工与会合。节点状态在运行时节点对象上，这里是图算法面。
/// 启动条件按节点输入侧方案一处判定，来源侧读数不创建节点。所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskRuntime : INodeContext
{
    private readonly WorkTask _task;
    private readonly NodeMaterializer _materializer;

    private static readonly ISourceState IdleSource = new UnmaterializedSource();

    private static readonly IInputProgress IdleProgress = new UnmaterializedProgress();

    internal TaskRuntime(WorkTask task)
    {
        _task = task;
        _materializer = new NodeMaterializer(task.Graph, task.Inputs, this);
        // 开始是流程的唯一来源，任务启动即已放行，下游就绪判定按它读数，先建出它的运行时对象
        _ = _materializer.Get(Graph.Start.Path);
    }

    /// <summary>任务锁定的流程执行视图。</summary>
    public INodeGraph Graph => _task.Graph;

    /// <summary>运行期节点物化器，快照投影按它读已实例化节点。</summary>
    internal NodeMaterializer Materializer => _materializer;

    /// <summary>按节点路径取运行时节点对象，未实例化时按图定义实例化。读写要求持有 Gate。</summary>
    public NodeRuntime this[NodePath path] => _materializer.Get(path);

    /// <summary>按节点路径取运行时执行节点对象，节点必须是执行节点。</summary>
    public RuntimeExecutable Executable(NodePath path) => (RuntimeExecutable)_materializer.Get(path);

    /// <summary>执行所属的运行时执行节点。要求持有 Gate。</summary>
    public RuntimeExecutable Node(IExecution execution) => Executable(execution.Input.Node);

    /// <summary>已实例化的运行时节点，未实例化时为空。判定与展示按它读数，不创建节点。</summary>
    public NodeRuntime? Materialized(NodePath node) => _materializer.Find(node);

    /// <summary>任务的流程编译视图，节点实现经它定位成员。</summary>
    INodeGraph INodeContext.Graph => Graph;

    /// <summary>某节点的输入侧方案，开始与不参与就绪判定的节点为空。</summary>
    public NodeInputPlan? Plan(NodePath node) => Graph.PlanOf(node);

    /// <summary>某节点的运行期节点，未实例化时按图定义建出，任务已取消时落在取消态。</summary>
    public NodeRuntime Runtime(NodePath node) => this[node];

    ISourceState IReadinessSources.Source(NodePath node) => State(node);

    /// <summary>按节点路径取来源状态，未实例化时给未放行的默认状态。</summary>
    private ISourceState State(NodePath node) => Materialized(node) ?? IdleSource;

    /// <summary>已实例化的执行节点，按执行节点表顺序。判定与展示按它读数，不创建节点。</summary>
    private IEnumerable<RuntimeExecutable> MaterializedExecutables() => _materializer.Executables();

    // ---- 就绪判定 ----

    /// <summary>派发执行的节点还有没有待启动的执行：节点侧状态先过，再按输入侧方案判定齐备与驱动。</summary>
    public bool CanStart(RuntimeExecutable node)
    {
        if (node.HasActive || node.Canceled || node.Blocked)
        {
            return false;
        }

        return Graph.PlanOf(node.Path) is { } plan && plan.Ready(node, this);
    }

    /// <summary>执行节点方案上的执行次数上限，输入节点没有方案，也就没有上限。</summary>
    private int? LimitOf(RuntimeExecutable node) => Graph.PlanOf(node.Path)?.MaxExecutions;

    /// <summary>该执行路径已达方案上的执行次数上限，达到后不再启动新执行。</summary>
    private bool AtLimit(RuntimeExecutable node) =>
        LimitOf(node) is { } limit && node.Executions >= limit;

    /// <summary>把执行节点启动一次。调用者应先用 <see cref="CanStart"/> 判定。
    /// 强制重跑沿用上一次的输入侧组，其余启动取驱动判定的组。</summary>
    private void Start(RuntimeExecutable node)
    {
        if (Graph.PlanOf(node.Path) is not { } plan)
        {
            return;
        }

        int group = node.RerunRequested && node.LastInputGroup is { } last
            ? last
            : plan.DrivenGroup(node, this) ?? node.LastInputGroup ?? 0;
        node.RecordInputGroup(group);
        node.AddActive();
        node.ClearRerun();
        node.Consume(plan, this);
    }

    // ---- 发布与作废 ----

    /// <summary>作废已发表产出并刷新祖先容器，返工起点用它让下游重新等待。</summary>
    private void InvalidateNode(RuntimeExecutable node)
    {
        // 版本驱动的节点没有“未产出”这一状态可回退，作废即记下一次强制重启
        node.RequestRerun();
        node.Invalidate();
        RefreshContainers(node.Path);
    }

    /// <summary>作废已发表产出或清除自驱型节点的失败，宿主返工入口按节点路径定位节点。</summary>
    public void Invalidate(NodePath node)
    {
        NodeRuntime runtime = _materializer.Get(node);
        if (runtime is RuntimeExecutable executable)
        {
            InvalidateNode(executable);
            return;
        }

        runtime.Retry();
    }

    // ---- 返工 ----

    /// <summary>顺着出边要通知的下游执行节点路径，去重保持顺序。</summary>
    public IReadOnlyList<NodePath> Downstream(NodeRuntime node) => Downstream(node.Path);

    /// <summary>顺着出边要通知的下游执行节点路径，去重保持顺序。</summary>
    public IReadOnlyList<NodePath> Downstream(NodePath node) =>
        [.. Graph.Outgoing(node).Select(edge => edge.Target).Distinct()];

    // ---- 容器推进 ----

    /// <summary>沿着节点所在的祖先链刷新容器：来源版本账有变化且内容齐备时推进内容版本并广播出边。
    /// 返回内容版本变化的容器出边目标。</summary>
    public IReadOnlyList<NodePath> RefreshContainers(NodePath path)
    {
        List<NodePath> notify = [];
        while (Graph.ContainerOf(path) is { } parent && _materializer.Get(parent.Path) is RuntimeContainer container)
        {
            if (container.ContentReady && container.PublishIfChanged())
            {
                notify.AddRange(Downstream(container));
            }

            path = container.Path;
        }

        return notify;
    }

    // ---- 汇总 ----

    /// <summary>是否有节点停在阻塞：执行节点失败或未收口，自驱型节点激活失败。</summary>
    public bool HasBlocked =>
        _materializer.Materialized.Any(node => node.Snapshot().State == NodeState.Blocked);

    /// <summary>还有在跑的执行、等待外部输入的节点、可启动或可由来源新版本驱动的节点，任务就没走完，
    /// 已发布产出的循环节点仍可由新版本再次启动。被抑制消耗后不再有活动的节点不阻止完成。随任务取消的节点不计。</summary>
    public bool HasWork =>
        Graph.ExecutableNodes.Any(executable => Materialized(executable.Path) switch
        {
            RuntimeExecutable node => node.HasActive || CanStart(node),
            NodeRuntime node => node.AwaitingInput || Drivable(node),
            _ => Startable(executable),
        });

    /// <summary>已实例化的非派发节点当前是否有重跑意图或可由来源新版本驱动。</summary>
    private bool Drivable(NodeRuntime node) =>
        node is IInputProgress progress
        && (progress.ForceRerun
            || (Graph.PlanOf(node.Path) is { } plan && plan.DrivenGroup(progress, this) is not null));

    /// <summary>尚未实例化的执行节点是否已具启动条件：与启动判定共用同一实现，未实例化的来源按未放行求值。
    /// 未实例化说明推进循环还没把它激活。</summary>
    private bool Startable(ExecutableNode executable) =>
        Graph.PlanOf(executable.Path) is { } plan && plan.Ready(IdleProgress, this);

    /// <summary>给定节点的产出已变，要重新评估的目标：取下游并刷新祖先容器，去重保持顺序。要求持有 Gate。</summary>
    public IReadOnlyList<NodePath> Notify(IReadOnlyList<NodePath> nodes)
    {
        List<NodePath> notify = [];
        foreach (NodePath node in nodes)
        {
            notify.AddRange(Downstream(node));
            notify.AddRange(RefreshContainers(node));
        }

        return [.. notify.Distinct()];
    }

    /// <summary>取消任务：置任务取消位，已实例化的节点全部进入取消态，迟实例化的节点创建即取消。</summary>
    public void Cancel() => _materializer.CancelAll();

    // ---- 执行收口 ----

    /// <summary>执行收口的决策结果：要通知的执行节点。</summary>
    public sealed record SettlePlan(IReadOnlyList<NodePath> Notify);

    /// <summary>执行收口：失败与未收口先停驻阻塞，通过后发布产出并给出下游通知。
    /// 接收器先经 ReleaseActive 再进入。</summary>
    public SettlePlan Settle(RuntimeExecutable node, Execution execution)
    {
        if (execution.State != ExecutionState.Succeeded)
        {
            return Block(node, execution);
        }

        // 上报无法完成的当轮不产出，直接停驻并把原因留给返工
        if (node.CannotCompleteReason(execution.Id) is { } cannotComplete)
        {
            execution.MarkUncollected($"模型报告无法完成：{cannotComplete}");

            return Block(node, execution);
        }

        ExecutableNode executable = node.Executable;
        if (node.Shortfall(execution.Id).Count > 0)
        {
            execution.MarkUncollected("输出端口内容未交回，本步未收口。");
            return Block(node, execution);
        }

        // 输出校验不通过即不发布，节点停驻等返工
        IReadOnlyDictionary<PortName, string>? outputs = node.SubmittedPorts(execution.Id)?
            .ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? string.Empty);
        if (executable.Spec.Validate is { } validation
            && !validation.Passes(executable.Outputs, outputs))
        {
            execution.MarkUncollected("输出校验不通过，本步未收口。");

            return Block(node, execution);
        }

        node.Publish(execution);
        _task.NotifyExecutionSettled(execution);

        return Settled(node, Downstream(node));
    }

    /// <summary>执行停驻阻塞：判定先交回等待方，再把这个执行节点记为阻塞并核算执行上限。</summary>
    private SettlePlan Block(RuntimeExecutable node, Execution execution)
    {
        _task.NotifyExecutionSettled(execution);
        node.EnterBlocked([node.Path]);
        NoteLimitReached(node);

        return new SettlePlan([]);
    }

    /// <summary>失败停驻的路径已达执行上限时记录提示，后续返工不会放行。</summary>
    private void NoteLimitReached(RuntimeExecutable node)
    {
        if (LimitOf(node) is not { } limit || node.Executions < limit)
        {
            return;
        }

        _task.Journal.Append(new ErrorEntry($"节点 {node.Path} 的执行次数已达上限 {limit} 次，不再重跑。"));
    }

    /// <summary>产出发布后的共同收口：把祖先容器临近齐备的出边目标并入通知。</summary>
    private SettlePlan Settled(RuntimeExecutable node, IReadOnlyList<NodePath> downstream)
    {
        IReadOnlyList<NodePath> containers = RefreshContainers(node.Path);

        return new SettlePlan([.. downstream, .. containers]);
    }

    // ---- 推进评估 ----

    /// <summary>一个节点被激活后的评估：非派发型节点在其中自行处理并报告版本与失败，派发型的按就绪条件启动一次执行。
    /// 返回要通知的下游与要启动的执行。要求持有 Gate。</summary>
    public EvaluateResult Evaluate(NodeRuntime node)
    {
        if (Graph.ShapeOf(node.Path) is not { } shape)
        {
            return new EvaluateResult([], false);
        }

        if (shape.Organizes)
        {
            return new EvaluateResult([], false);
        }

        if (!shape.DispatchesExecutions)
        {
            // Trigger 为 false 的组先消耗；消耗后没有其他可驱动组时不再激活节点
            if (node is IInputProgress progress
                && Graph.PlanOf(node.Path) is { } plan
                && ConsumeSuppressed(node, progress, plan)
                && plan.DrivenGroup(progress, this) is null)
            {
                return new EvaluateResult([], false);
            }

            long revision = node.Revision;
            node.Activate(this);

            if (node.TakeFailure() is { Length: > 0 } failure)
            {
                _task.Journal.Append(new ErrorEntry($"{node.Path}：{failure}"));
                _task.Registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{_task.Id} 停在节点 {node.Path.Label}，{failure}"));
                _task.Touch();
            }

            if (node.Revision == revision)
            {
                return new EvaluateResult([], false);
            }

            _task.Touch();

            return new EvaluateResult(Notify([node.Path]), false);
        }

        if (node is not RuntimeExecutable executable)
        {
            return new EvaluateResult([], false);
        }

        if (!CanStart(executable))
        {
            if (Graph.PlanOf(executable.Path) is { } plan)
            {
                ConsumeSuppressed(executable, executable, plan);
            }

            return new EvaluateResult([], false);
        }

        Start(executable);

        return new EvaluateResult([], true);
    }

    /// <summary>消耗所有由新版本驱动但存在 Trigger 为 false 的启动组，每组写一条信息记录。节点在跑、阻塞、取消、强制重跑或达到执行上限时不消耗。</summary>
    private bool ConsumeSuppressed(NodeRuntime node, IInputProgress progress, NodeInputPlan plan)
    {
        if (!AcceptsConsumption(node, progress, plan))
        {
            return false;
        }

        bool consumed = false;
        while (plan.SuppressedGroup(progress, this) is { } group)
        {
            progress.Consume(group, this);
            NoteSuppressed(node, group);
            consumed = true;
        }

        return consumed;
    }

    /// <summary>节点此刻是否接受抑制组消耗：在跑、阻塞、取消、强制重跑或达到执行上限时保留来源版本。</summary>
    private static bool AcceptsConsumption(NodeRuntime node, IInputProgress progress, NodeInputPlan plan)
    {
        if (node.Canceled || progress.ForceRerun || node.Snapshot().State == NodeState.Blocked)
        {
            return false;
        }

        if (progress.Executions >= plan.MaxExecutions)
        {
            return false;
        }

        return node is not RuntimeExecutable executable || !executable.HasActive;
    }

    /// <summary>记录一次抑制组消耗：给出节点、未触发的来源与端口。</summary>
    private void NoteSuppressed(NodeRuntime node, InputGroup group)
    {
        string ports = string.Join('、', group.Bindings
            .Where(binding => !binding.Triggers(State(binding.Source)))
            .Select(binding => $"{binding.Source}@{binding.Port}"));
        string message = $"{_task.Id} 的节点 {node.Path.Label} 有启动组未触发：{ports} 的产出为 false，本组已消耗，不再等待。";

        _task.Journal.Append(new InfoEntry(message));
        _task.Registry.Report(new ExecutionNotice(NoticeLevel.Information, message));
        _task.Touch();
    }

    /// <summary>一次评估的结果：要通知的下游执行节点路径与是否已启动执行。</summary>
    public readonly record struct EvaluateResult(IReadOnlyList<NodePath> Notify, bool Started);

    /// <summary>任务启动的第一批：开始已放行，由它的直连下游起步。返回前全部实例化。</summary>
    public IReadOnlyList<NodePath> Roots() =>
        [.. Graph.Outgoing(Graph.Start.Path).Select(edge => _materializer.Get(edge.Target).Path)];

    /// <summary>一个返工目标：停住的节点，与它要重跑的节点。自驱型节点的失败目标与自身相同。</summary>
    public readonly record struct ReworkTarget(NodePath Blocked, NodePath Node);

    /// <summary>计算返工会作废并重跑的目标：各停在阻塞的执行节点记录的目标，以及阻塞的自驱型节点自身。顺序按执行节点表，自驱型节点随后按实例化顺序。不改变状态。要求持有 Gate。</summary>
    public IReadOnlyList<ReworkTarget> PlanRework()
    {
        List<ReworkTarget> targets = [];
        foreach (RuntimeExecutable node in MaterializedExecutables())
        {
            foreach (NodePath target in node.ReworkTargets)
            {
                if (AtLimit(Executable(target)))
                {
                    continue;
                }

                targets.Add(new ReworkTarget(node.Path, target));
            }
        }

        foreach (NodeRuntime node in _materializer.Materialized)
        {
            if (node is not RuntimeExecutable && node.Snapshot().State == NodeState.Blocked)
            {
                targets.Add(new ReworkTarget(node.Path, node.Path));
            }
        }

        return targets;
    }

    /// <summary>解除一个执行节点的阻塞：移除本次返工处理的目标，剩余目标保持阻塞，全部处理后解除阻塞。自驱型节点没有目标表，失败由 <see cref="Invalidate"/> 清除。</summary>
    public void Unblock(NodePath node, IReadOnlyList<NodePath> handled)
    {
        if (Materialized(node) is RuntimeExecutable executable)
        {
            executable.FinishRework(handled);
        }
    }

    /// <summary>未实例化节点的来源状态：没有产出、版本为零。</summary>
    private sealed class UnmaterializedSource : ISourceState
    {
        public bool Released => false;

        public long Revision => 0;
    }

    /// <summary>未实例化节点的进展：没有消费、没有执行次数。</summary>
    private sealed class UnmaterializedProgress : IInputProgress
    {
        public bool ForceRerun => false;

        public long ConsumedRevision(ConsumptionKey key) => 0;

        public void Consume(InputGroup group, IReadinessSources sources)
        {
        }

        public int Executions => 0;
    }
}

