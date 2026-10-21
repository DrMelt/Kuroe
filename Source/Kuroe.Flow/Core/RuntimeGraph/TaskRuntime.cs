using System.Text.RegularExpressions;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.RuntimeNodes;
using Kuroe.Flow.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;
using ContainerNode = Kuroe.Flow.Shared.StaticGraph.ContainerNode;
using StartNode = Kuroe.Flow.Shared.StaticGraph.StartNode;

namespace Kuroe.Flow.RuntimeGraph;

/// <summary>按图驱动的执行推进：激活、发布、返工与会合。节点状态在运行时节点对象上，这里是图算法面。
/// 启动条件按节点输入侧方案一处判定，来源侧读数不创建节点。所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskRuntime : INodeContext
{
    private readonly WorkTask _task;
    private bool _taskCanceled;

    /// <summary>已实例化的根级运行时节点：开始与流程根节点。容器的直接子节点由容器自己持有。</summary>
    private readonly List<NodeRuntime> _roots = [];

    private static readonly ISourceState IdleSource = new UnmaterializedSource();

    private static readonly IInputProgress IdleProgress = new UnmaterializedProgress();

    internal TaskRuntime(WorkTask task)
    {
        _task = task;
        // 开始是流程的唯一来源，任务启动即已放行，下游就绪判定按它读数，先建出它的运行时对象
        _ = Ensure(Graph.Start.Path);
    }

    /// <summary>任务锁定的流程编译视图。</summary>
    public NodeGraph Graph => _task.Graph;

    /// <summary>已实例化的运行时节点数，诊断与测试观测懒实例化的边界。</summary>
    internal int MaterializedCount => _roots.Sum(NodeCount);

    /// <summary>节点自身与它已实例化的子树节点数。</summary>
    private static int NodeCount(NodeRuntime node) =>
        1 + (node is RuntimeContainer container ? container.Children.Sum(NodeCount) : 0);

    /// <summary>按节点路径取运行时节点对象，未实例化时按图定义实例化。读写要求持有 Gate。</summary>
    public NodeRuntime this[NodePath path] => Ensure(path);

    /// <summary>按节点路径取运行时执行节点对象，节点必须是执行节点。</summary>
    public RuntimeExecutable Executable(NodePath path) => (RuntimeExecutable)Ensure(path);

    /// <summary>执行所属的运行时执行节点。要求持有 Gate。</summary>
    public RuntimeExecutable Node(IExecution execution) => Executable(execution.Input.Node);

    /// <summary>已实例化的运行时节点，未实例化时为空。判定与展示按它读数，不创建节点。</summary>
    public NodeRuntime? Materialized(NodePath node) => Walk(node, materialize: false);

    /// <summary>任务的流程编译视图，节点实现经它定位成员。</summary>
    INodeGraph INodeContext.Graph => Graph;

    /// <summary>某节点的输入侧方案，开始与不参与就绪判定的节点为空。</summary>
    public NodeInputPlan? Plan(NodePath node) => Graph.PlanOf(node);

    /// <summary>某节点的运行期节点，未实例化时按图定义建出，任务已取消时落在取消态。</summary>
    public NodeRuntime Runtime(NodePath node) => this[node];

    /// <summary>沿路径逐段下钻：命中已实例化的节点即取它，materialize 为真时建出缺失的段，为假时给空。
    /// 中间段必须是容器，子节点登记在所在容器上。</summary>
    private NodeRuntime? Walk(NodePath path, bool materialize)
    {
        IReadOnlyList<NodeRuntime> level = _roots;
        RuntimeContainer? owner = null;
        NodeRuntime? current = null;
        for (int depth = 1; depth <= path.Depth; depth++)
        {
            NodePath prefix = new([.. path.Levels.Take(depth)]);
            current = level.FirstOrDefault(node => node.Path.Label == prefix.Label);
            if (current is null)
            {
                if (!materialize)
                {
                    return null;
                }

                current = Create(prefix);
                if (owner is null)
                {
                    _roots.Add(current);
                }
                else
                {
                    owner.Add(current);
                }

                if (_taskCanceled)
                {
                    current.Cancel();
                }
            }

            if (depth == path.Depth)
            {
                return current;
            }

            if (current is not RuntimeContainer container)
            {
                throw new ArgumentException($"路径 {path} 的中间段不是容器，无法下钻。");
            }

            owner = container;
            level = container.Children;
        }

        return current;
    }

    /// <summary>路径对应的运行时节点，未实例化时按图定义建出。</summary>
    private NodeRuntime Ensure(NodePath path) => Walk(path, materialize: true)!;

    /// <summary>按形状建立节点对象：开始给任务目标，容器与派发执行的节点由引擎建立，其余由类型自建。</summary>
    private NodeRuntime Create(NodePath path) =>
        Graph[path] switch
        {
            StartNode start => new RuntimeStart(start, _task.Goal),
            ExecutableNode executable when executable.Type.Shape.DispatchesExecutions => new RuntimeExecutable(executable),
            ContainerNode container when container.Type.Shape.Organizes => new RuntimeContainer(container, this),
            ContainerNode container => container.Type.Create(container, this),
            ExecutableNode executable => executable.Type.Create(executable, this),
            _ => throw new InvalidOperationException($"节点 {path} 没有类型，无法建立运行期对象。"),
        };

    /// <summary>已实例化的运行时节点，先根序。汇总与取消按它读数，不创建节点。</summary>
    private IEnumerable<NodeRuntime> MaterializedNodes => _roots.SelectMany(Subtree);

    /// <summary>节点与它已实例化的子树节点。</summary>
    private static IEnumerable<NodeRuntime> Subtree(NodeRuntime node) =>
        node is RuntimeContainer container ? [node, .. container.Children.SelectMany(Subtree)] : [node];

    ISourceState IReadinessSources.Source(NodePath node) => Materialized(node) ?? IdleSource;

    /// <summary>已实例化的执行节点，按执行节点表顺序。判定与展示按它读数，不创建节点。</summary>
    private IEnumerable<RuntimeExecutable> MaterializedExecutables() =>
        Graph.ExecutableNodes.Select(executable => Materialized(executable.Path)).OfType<RuntimeExecutable>();

    /// <summary>节点在快照里的一刻状态：节点自己给出，未实例化的节点按默认待办报告。</summary>
    public ExecutableStateSnapshot SnapshotState(NodePath path) => Materialized(path)?.Snapshot()
        ?? new ExecutableStateSnapshot(path, NodeState.Pending);

    // ---- 就绪判定 ----

    /// <summary>派发执行的节点还有没有待启动的执行：节点侧状态先过，再按输入侧方案判定齐备与驱动。</summary>
    public bool CanStart(RuntimeExecutable node)
    {
        if (node.HasActive || node.Canceled || node.Blocked)
        {
            return false;
        }

        return Graph.PlanOf(node.Path) is { } plan && NodeReadiness.Ready(plan, node, this);
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
            : NodeReadiness.DrivenGroup(plan, node, this) ?? node.LastInputGroup ?? 0;
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

    /// <summary>作废已发表产出，宿主返工入口按节点路径定位节点。</summary>
    public void Invalidate(NodePath node) => InvalidateNode(Executable(node));

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
        while (Graph.ContainerOf(path) is { } parent && Ensure(parent.Path) is RuntimeContainer container)
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

    /// <summary>是否有执行节点停在等待返工。</summary>
    public bool HasBlocked => MaterializedNodes.OfType<RuntimeExecutable>().Any(node => node.Blocked);

    /// <summary>还有在跑的执行、可启动的执行节点或尚未放行的节点，任务就没走完。随任务取消的节点不计。</summary>
    public bool HasWork =>
        Graph.ExecutableNodes.Any(executable => Materialized(executable.Path) switch
        {
            RuntimeExecutable node => !node.Released || node.HasActive || CanStart(node),
            NodeRuntime node => !node.Released && !node.Canceled,
            _ => Startable(executable),
        });

    /// <summary>尚未实例化的执行节点是否已具启动条件：与启动判定共用同一实现，未实例化的来源按未放行求值。
    /// 未实例化说明推进循环还没把它激活。</summary>
    private bool Startable(ExecutableNode executable) =>
        Graph.PlanOf(executable.Path) is { } plan && NodeReadiness.Ready(plan, IdleProgress, this);

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
    public void Cancel()
    {
        _taskCanceled = true;
        foreach (NodeRuntime node in MaterializedNodes)
        {
            node.Cancel();
        }
    }

    // ---- 执行收口 ----

    /// <summary>执行收口的决策结果：要通知的执行节点。</summary>
    public sealed record SettlePlan(IReadOnlyList<NodePath> Notify);

    /// <summary>本轮该交回但尚未交回的产出：缺哪些命名端口。</summary>
    public sealed record ExecutionShortfall(IReadOnlyList<PortName> Ports);

    /// <summary>本轮该交回但尚未交回的产出，按节点声明的需交回端口判定。</summary>
    public static ExecutionShortfall Shortfall(RuntimeExecutable node, ExecutionId execution)
    {
        IReadOnlyDictionary<PortName, string>? values = node.Ports(execution);

        IReadOnlyList<PortName> ports = [.. node.Executable.RequiredPorts
            .Select(port => port.Name)
            .Where(name => values?.ContainsKey(name) != true)];

        return new ExecutionShortfall(ports);
    }

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
        if (Shortfall(node, execution.Id).Ports.Count > 0)
        {
            execution.MarkUncollected("输出端口内容未交回，本步未收口。");
            return Block(node, execution);
        }

        // 输出校验不通过即不发布，节点停驻等返工
        if (!PassesValidation(executable.Spec.Validate, executable, node.Ports(execution.Id)))
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

    /// <summary>输出校验判定：不改动任何状态，只给出端口产出是否合格。校验对象是各声明端口按声明序拼接的文本。</summary>
    private static bool PassesValidation(
        OutputValidation? validation,
        ExecutableNode executable,
        IReadOnlyDictionary<PortName, string>? portValues)
    {
        if (validation is null)
        {
            return true;
        }

        string? result = JoinedPorts(executable, portValues);

        return validation.Predicate switch
        {
            ValidationPredicate.NonEmpty => result is { Length: > 0 },
            ValidationPredicate.TextContains => Contains(result, validation.Argument),
            ValidationPredicate.TextNot => result is { Length: > 0 } && !Contains(result, validation.Argument),
            ValidationPredicate.TextEquals => string.Equals(result, validation.Argument, StringComparison.Ordinal),
            ValidationPredicate.Pattern => Matches(result, validation.Argument),
            _ => true,
        };
    }

    /// <summary>按声明顺序拼接端口值，端口未全部交回时为空。校验发生在端口完整性检查之后，缺失只可能缘于单端口空值。</summary>
    private static string? JoinedPorts(ExecutableNode executable, IReadOnlyDictionary<PortName, string>? portValues)
    {
        if (portValues is null)
        {
            return null;
        }

        string[] parts = [.. executable.Outputs
            .Where(port => port.Type == PortType.Text)
            .Select(port => portValues.GetValueOrDefault(port.Name))
            .Where(value => value is { Length: > 0 })
            .Cast<string>()];

        return parts.Length == 0 ? null : string.Join("\n", parts);
    }

    private static bool Contains(string? text, string? needle) =>
        needle is { Length: > 0 } && text is not null && text.Contains(needle, StringComparison.Ordinal);

    /// <summary>正则匹配：产物为空或匹配超时一律视为不通过。</summary>
    private static bool Matches(string? text, string? pattern)
    {
        if (text is null || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        try
        {
            return Regex.IsMatch(text, pattern, RegexOptions.None, TimeSpan.FromSeconds(1));
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
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

    /// <summary>一个节点被激活后的评估：非派发型节点在其中自行处理，派发型的按就绪条件启动一次执行。
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
            node.Activate(this);

            return new EvaluateResult([], false);
        }

        if (node is not RuntimeExecutable executable || !CanStart(executable))
        {
            return new EvaluateResult([], false);
        }

        Start(executable);

        return new EvaluateResult([], true);
    }

    /// <summary>一次评估的结果：要通知的下游执行节点路径与是否已启动执行。</summary>
    public readonly record struct EvaluateResult(IReadOnlyList<NodePath> Notify, bool Started);

    /// <summary>任务启动的第一批：开始已放行，由它的直连下游起步。返回前全部实例化。</summary>
    public IReadOnlyList<NodePath> Roots() =>
        [.. Graph.Outgoing(Graph.Start.Path).Select(edge => Ensure(edge.Target).Path)];

    // ---- 容器快照 ----

    /// <summary>各容器在快照里的一刻状态：内容齐备情况与成员活动。快照不实例化节点。</summary>
    public IReadOnlyList<ContainerSnapshot> ContainerSnapshots()
    {
        List<ContainerSnapshot> snapshots = [];
        foreach (ContainerNode container in Graph.Containers)
        {
            snapshots.Add(Materialized(container.Path) is RuntimeContainer runtime
                ? ContainerSnapshotOf(runtime)
                : UnmaterializedSnapshotOf(container));
        }

        return snapshots;
    }

    /// <summary>未实例化容器的一刻状态：内容尚未齐备，状态全由已实例化的成员活动决定。</summary>
    private ContainerSnapshot UnmaterializedSnapshotOf(ContainerNode container) =>
        new(container.Path, ContainerActivityState(container), ChildPaths(container));

    /// <summary>已实例化容器的一刻状态。成员状态只在已实例化成员上读，未实例化成员视为没有活动，快照不实例化节点。</summary>
    private ContainerSnapshot ContainerSnapshotOf(RuntimeContainer container)
    {
        NodeState state;
        if (container.Canceled)
        {
            state = NodeState.Canceled;
        }
        else if (container.Released)
        {
            state = NodeState.Done;
        }
        else
        {
            state = ContainerActivityState(container.Container);
        }

        return new ContainerSnapshot(container.Path, state, ChildPaths(container.Container));
    }

    /// <summary>容器的直接子节点路径，先根序。</summary>
    private static IReadOnlyList<NodePath> ChildPaths(ContainerNode container) =>
        [.. container.Children.Select(child => child.Path)];

    /// <summary>容器里已实例化成员的活动状态：有在跑或已产出即运行中，都没有即待办。</summary>
    private NodeState ContainerActivityState(ContainerNode container)
    {
        IReadOnlyList<NodePath> members = Graph.ExecutablesIn(container.Path);

        return members.Any(MemberActive) ? NodeState.Running : NodeState.Pending;
    }

    /// <summary>成员执行节点是否有在跑的执行或已产出，未实例化的成员没有活动。</summary>
    private bool MemberActive(NodePath executable) =>
        Materialized(executable) is RuntimeExecutable node
            && (node.HasActive || node.PublishedExecution is not null);

    /// <summary>一个返工目标：停在阻塞的执行节点，与它记下要重跑的节点。</summary>
    public readonly record struct ReworkTarget(NodePath Blocked, NodePath Node);

    /// <summary>计算返工会作废并重跑的目标：各停在阻塞的执行节点记录的目标，配对其所在节点。顺序按执行节点表。不改变状态。要求持有 Gate。</summary>
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

        return targets;
    }

    /// <summary>解除一个执行节点的阻塞：移除本次返工处理的目标，剩余目标保持阻塞，全部处理后解除阻塞。要求持有 Gate。</summary>
    public void Unblock(NodePath node, IReadOnlyList<NodePath> handled) =>
        Executable(node).FinishRework(handled);

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

        public long ConsumedRevision(NodePath source) => 0;

        public int Executions => 0;
    }
}

