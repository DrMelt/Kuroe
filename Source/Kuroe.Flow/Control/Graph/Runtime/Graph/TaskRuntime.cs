using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Node.RuntimeGraph;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.RuntimeGraph.Ports;
using Kuroe.Flow.Node.RuntimeGraph.Readiness;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Node.StaticGraph.Graph;
using Kuroe.Flow.Node.StaticGraph.Nodes;
using Kuroe.Flow.Node.StaticGraph.Ports;

using Kuroe.Flow.Control.Graph.Nodes;
using ExecutableNode = Kuroe.Flow.Node.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Control.Graph;

/// <summary>一次执行中断后的结果，只由执行状态与节点限制决定。</summary>
public enum ExecutionInterruption
{
    /// <summary>正常收口，没有中断。</summary>
    None,

    /// <summary>执行在启动前被中断，不构成一次失败尝试。</summary>
    NotStarted,

    /// <summary>执行已启动并留下可恢复的恢复数据。</summary>
    Restorable,

    /// <summary>执行已启动，没有可用于恢复的数据。</summary>
    NonRestorable,
}

/// <summary>按图驱动的执行推进：激活、发布、重新执行与会合。节点状态在运行时节点对象上，这里是图算法面。
/// 启动条件按节点输入侧方案判定，不参与方案的节点在评估时按图上的常量依赖单独挡住。来源侧读数不创建节点。
/// 所有方法都要求调用者持有所属任务的 Gate。</summary>
internal sealed class TaskRuntime : INodeContext, INodeRestoreContext, IRuntimeConstantSource
{
    private readonly ITaskFlowContext _task;
    private readonly GraphFlowState _state;
    private readonly NodeMaterializer _materializer;
    private readonly StaticPortValues _staticPorts;
    private readonly RuntimeStartSource _startSource;
    private readonly IExecutionRuntime _executions;
    private readonly Dictionary<NodePath, RestorePlan> _restorePlans = [];

    private static readonly ISourceState IdleSource = new UnmaterializedSource();

    private static readonly IInputProgress IdleProgress = new UnmaterializedProgress();

    internal TaskRuntime(ITaskFlowContext task, GraphFlowState state, IExecutionRuntime executions)
    {
        _task = task;
        _state = state;
        _executions = executions;
        _materializer = new NodeMaterializer(state.Flow.Graph, this);
        _staticPorts = new StaticPortValues(state.Flow.Graph, state.StartPorts, this);
        _startSource = new RuntimeStartSource(state.Flow.Graph.StartPorts, state.StartPorts);
    }

    /// <summary>任务锁定的流程执行视图。</summary>
    public NodeGraph Graph => _state.Flow.Graph;

    /// <summary>运行期节点物化器，快照投影按它读已实例化节点。</summary>
    internal NodeMaterializer Materializer => _materializer;

    /// <summary>任务内的静态端口解析，执行目标解析与激活输入共用。</summary>
    internal StaticPortValues StaticPorts => _staticPorts;

    /// <summary>按节点路径取运行时节点对象，未实例化时按图定义实例化。读写要求持有 Gate。</summary>
    public NodeRuntime this[NodePath path] => _materializer.Get(path);

    /// <summary>按节点路径取运行时执行节点对象，节点必须是执行节点。</summary>
    public RuntimeExecutable Executable(NodePath path) => (RuntimeExecutable)_materializer.Get(path);

    /// <summary>执行所属的运行时执行节点。要求持有 Gate。</summary>
    public RuntimeExecutable Node(IGraphExecution execution) => Executable(execution.Input.Node);

    /// <summary>已实例化的运行时节点，未实例化时为空。判定与展示按它读数，不创建节点。</summary>
    public NodeRuntime? Materialized(NodePath node) => _materializer.Find(node);

    /// <summary>某节点的输入侧方案，没有方案时为空。</summary>
    public NodeInputPlan? Plan(NodePath node) => Graph.PlanOf(node);

    /// <summary>按存档恢复节点状态。先装载端口历史，再按路径深度升序恢复节点。</summary>
    internal void Restore(IReadOnlyList<NodeRecord> nodes, IReadOnlyList<PortPublicationRecord> ports)
    {
        foreach (IGrouping<NodePath, PortPublicationRecord> group in ports.GroupBy(record => record.Node))
        {
            ((IPortRecordSource)this[group.Key]).RestorePorts([.. group]);
        }

        foreach (NodeRecord record in nodes.OrderBy(node => node.Path.Depth))
        {
            ((INodeRecordSource)this[record.Path]).RestoreRecord(record, this);
        }
    }

    /// <summary>按执行号取已恢复的执行，节点恢复已发布产出时使用。</summary>
    IGraphExecution? INodeRestoreContext.Execution(ExecutionId id) =>
        _state.Executions.FirstOrDefault(execution => execution.Id == id);

    /// <summary>某节点的运行期节点，未实例化时按图定义建出，任务已取消时落在取消态。</summary>
    public NodeRuntime Runtime(NodePath node) => this[node];

    ISourceState IReadinessSources.Source(BindingSource source) => State(source);

    /// <summary>按来源取状态。开始来源由任务持有，节点来源未实例化时给未放行的默认状态。</summary>
    private ISourceState State(BindingSource source)
    {
        if (source.IsStart)
        {
            return _startSource;
        }

        return source.Node is { } node ? Materialized(node) ?? IdleSource : IdleSource;
    }

    /// <summary>来源端口的内容产出。</summary>
    public IReadOnlyList<ContextValue> OutputValues(BindingSource source)
    {
        if (source.IsStart)
        {
            return source.Port is { } startPort ? _startSource.OutputValues(startPort) : [];
        }

        if (source.Node is { } node && source.Port is { } port)
        {
            return this[node].OutputValues(port);
        }

        return [];
    }

    /// <summary>已实例化的执行节点，按执行节点表顺序。判定与展示按它读数，不创建节点。</summary>
    private IEnumerable<RuntimeExecutable> MaterializedExecutables() => _materializer.Executables();

    /// <summary>节点唯一一次执行产出的常量是否已就绪。发布后保留，任务取消后不再读取。</summary>
    bool IRuntimeConstantSource.Available(NodePath node, PortName port) =>
        Materialized(node) is RuntimeExecutable { Canceled: false } runtime
        && runtime.Progress.Published
        && runtime.Executable.FindPort(port) is not null;

    /// <summary>取节点唯一一次执行产出的常量文本，节点尚未发布或已取消时为空。</summary>
    string? IRuntimeConstantSource.Text(NodePath node, PortName port) =>
        Materialized(node) is RuntimeExecutable { Canceled: false } runtime && runtime.Progress.Published
            ? runtime.PortText(port)
            : null;

    // ---- 就绪判定 ----

    /// <summary>派发执行的节点还有没有待启动的执行：按输入侧方案判定静态依赖、齐备与驱动。</summary>
    public bool CanStart(RuntimeExecutable node)
    {
        if (node.HasActive || node.Canceled || node.Block is not null)
        {
            return false;
        }

        return Graph.PlanOf(node.Path) is { } plan
            && plan.Ready(node, this);
    }

    /// <summary>执行节点方案上的执行次数上限，输入节点没有方案，也就没有上限。</summary>
    private int? LimitOf(RuntimeExecutable node) => Graph.PlanOf(node.Path)?.MaxExecutions;

    /// <summary>该执行路径已达方案上的执行次数上限，达到后不再启动新执行。</summary>
    private bool AtLimit(RuntimeExecutable node) =>
        LimitOf(node) is { } limit && node.Executions >= limit;

    /// <summary>把执行节点启动一次，并返回本次装入的输入。调用者应先用 <see cref="CanStart"/> 判定。
    /// 强制重跑沿用上一次的输入侧组，其余启动取驱动判定的组。</summary>
    private NodeInputValues Start(RuntimeExecutable node)
    {
        if (Graph.PlanOf(node.Path) is not { } plan)
        {
            return WithStatic(node.Executable, NodeInputValues.Empty);
        }

        if (plan.StaticOnly)
        {
            node.AddActive();
            node.ClearRerun();
            node.Consume(plan, this);

            return WithStatic(node.Executable, NodeInputValues.Empty);
        }

        int? group = node.Progress.ForceRerun && node.Progress.LastInputGroup is { } last
            ? last
            : plan.DrivenGroup(node, this) ?? node.Progress.LastInputGroup;

        if (group is not { } index || index < 0 || index >= plan.Groups.Count)
        {
            throw new InvalidOperationException($"节点 {node.Path} 没有可用的启动组，不能启动执行。");
        }

        node.Progress.RecordGroup(index);
        NodeInputValues inputs = NodeInputValues.From(plan.Groups[index], this);
        node.AddActive();
        node.ClearRerun();
        node.Consume(plan, this);

        return WithStatic(node.Executable, inputs);
    }

    /// <summary>把执行节点的静态端口文本附加到本次激活的输入。</summary>
    private NodeInputValues WithStatic(ExecutableNode node, NodeInputValues inputs) =>
        inputs.WithStatic(_staticPorts.For(node));

    // ---- 发布与作废 ----

    /// <summary>作废执行节点的产出并刷新祖先容器，重新执行起点用它让下游重新等待。已发布的 Once 节点不重跑。</summary>
    private void InvalidateNode(RuntimeExecutable node)
    {
        if (Frozen(node.Path))
        {
            return;
        }

        InvalidateRestore(node.Path);
        node.RequestRerun();
        node.Invalidate();
    }

    /// <summary>作废已发表产出，宿主重新执行入口按节点路径定位节点。已发布的 Once 节点不重跑。</summary>
    public void Invalidate(NodePath node)
    {
        if (Frozen(node))
        {
            return;
        }

        if (_materializer.Get(node) is RuntimeExecutable executable)
        {
            InvalidateNode(executable);
        }
    }

    // ---- 重新执行 ----

    /// <summary>顺着下游绑定要通知的执行节点路径，去重保持顺序。</summary>
    public IReadOnlyList<NodePath> Downstream(NodeRuntime node) => Downstream(node.Path);

    /// <summary>顺着下游绑定要通知的执行节点路径，去重保持顺序。</summary>
    public IReadOnlyList<NodePath> Downstream(NodePath node) =>
        [.. Graph.Outgoing(node).Select(binding => binding.Target).Distinct()];

    // ---- 容器推进 ----

    /// <summary>节点所在的祖先容器路径，按由近到远。容器发布由统一节点评估完成。</summary>
    public IReadOnlyList<NodePath> AncestorContainers(NodePath path)
    {
        List<NodePath> notify = [];
        while (Graph.ContainerOf(path) is { } parent)
        {
            notify.Add(parent.Path);
            path = parent.Path;
        }

        return notify;
    }

    // ---- 汇总 ----

    /// <summary>是否有节点停在阻塞：执行节点失败或未收口。</summary>
    public bool HasBlocked =>
        _materializer.Materialized.Any(node => node.Snapshot().State == NodeState.Blocked);

    /// <summary>还有在跑的执行、等待外部输入的节点、可启动或可由来源新版本驱动的节点、可发布的容器，任务就没走完，
    /// 已发布产出的循环节点仍可由新版本再次启动。被抑制消耗后不再有活动的节点不阻止完成。随任务取消的节点不计。</summary>
    public bool HasWork =>
        Graph.ExecutableNodes.Any(executable => Materialized(executable.Path) switch
        {
            RuntimeExecutable node => node.HasActive || CanStart(node),
            NodeRuntime node => node.AwaitingInput || Drivable(node) || StaticStartable(node),
            _ => Startable(executable),
        })
        || Graph.Containers.Any(ContainerNeedsEvaluation);

    /// <summary>容器是否有待执行的一次发布或待消费的抑制组。未实例化的容器按没有已消费版本与执行次数求值。</summary>
    private bool ContainerNeedsEvaluation(ContainerNode container)
    {
        if (Materialized(container.Path) is RuntimeContainer { Canceled: true })
        {
            return false;
        }

        IInputProgress progress = Materialized(container.Path)?.Progress ?? IdleProgress;
        if (Graph.PlanOf(container.Path) is not { } plan)
        {
            return false;
        }

        bool canConsume = progress.Executions < plan.MaxExecutions && !progress.ForceRerun;

        return plan.Ready(progress, this)
            || (canConsume && plan.SuppressedGroup(progress, this) is not null);
    }

    /// <summary>已实例化的静态输入节点是否等待激活：静态依赖已发布、输入尚未产出，取消与阻塞时不等待。</summary>
    private bool StaticStartable(NodeRuntime node) =>
        node.Node is ExecutableNode
        && !node.Canceled
        && !node.Released
        && node.Snapshot().State != NodeState.Blocked
        && Graph.PlanOf(node.Path) is { StaticOnly: true } plan
        && plan.Ready(node.Progress, this);

    /// <summary>已实例化的非派发节点当前是否有重跑意图或可由来源新版本驱动。</summary>
    private bool Drivable(NodeRuntime node) =>
        node.Progress.ForceRerun
        || (Graph.PlanOf(node.Path) is { } plan && plan.DrivenGroup(node.Progress, this) is not null);

    /// <summary>尚未实例化的执行节点是否已具启动条件：与启动判定共用同一实现，未实例化的来源按未放行求值。
    /// 未实例化说明推进循环还没把它激活。</summary>
    private bool Startable(ExecutableNode executable) =>
        Graph.PlanOf(executable.Path) is { } plan
        && plan.Ready(IdleProgress, this);

    /// <summary>给定节点的产出已变，要重新评估的目标：取下游、待评估的祖先容器与依赖它的节点，去重保持顺序。要求持有 Gate。</summary>
    public IReadOnlyList<NodePath> Notify(IReadOnlyList<NodePath> nodes)
    {
        List<NodePath> notify = [];
        foreach (NodePath node in nodes)
        {
            notify.AddRange(Downstream(node));
            notify.AddRange(AncestorContainers(node));
            notify.AddRange(Graph.Dependents(node));
        }

        return [.. notify.Distinct()];
    }

    /// <summary>取消任务：置任务取消位，已实例化的节点全部进入取消态，迟实例化的节点创建即取消。</summary>
    public void Cancel() => _materializer.CancelAll();

    // ---- 执行收口 ----

    /// <summary>执行收口的决策结果：要通知的执行节点、执行中断后的状态分类与本次建立的可恢复计划。</summary>
    public sealed record SettlePlan(
        IReadOnlyList<NodePath> Notify,
        ExecutionInterruption Interruption = ExecutionInterruption.None,
        RestorePlan? Restore = null);

    /// <summary>执行收口：未启动的中断单独分类，失败与未收口先停驻阻塞，通过后发布产出并给出下游通知。
    /// 接收器先经 ReleaseActive 再进入。终止任务的收口只通知等待方，不再阻塞或发布。</summary>
    public SettlePlan Settle(RuntimeExecutable node, GraphExecution execution)
    {
        if (_task.Lifecycle == TaskLifecycle.Terminated)
        {
            _task.NotifyExecutionSettled(execution.Unit);

            return new SettlePlan([]);
        }

        if (execution.State != ExecutionState.Succeeded)
        {
            return Block(node, execution);
        }

        // 内容产出的执行不经过端口交回，产出文本直接发布到承载文本的端口
        ExecutionOutput output = execution.Output ?? ExecutionOutput.FromPorts([]);
        if (output.Kind == ExecutionOutputKind.Content)
        {
            node.Publish(execution, output);
            _task.NotifyExecutionSettled(execution.Unit);

            return Settled(node, Downstream(node));
        }

        // 上报无法完成的当轮不产出，直接停驻并把原因留给重新执行
        if (execution.Contract.CannotCompleteReason is { } cannotComplete)
        {
            _executions.Reject(execution.Unit, $"模型报告无法完成：{cannotComplete}");

            return Block(node, execution);
        }

        if (node.OutstandingPorts(execution).Count > 0)
        {
            _executions.Reject(execution.Unit, "输出端口内容未交回，本步未收口。");
            return Block(node, execution);
        }

        node.Publish(execution, output);
        _task.NotifyExecutionSettled(execution.Unit);

        return Settled(node, Downstream(node));
    }

    /// <summary>执行停驻阻塞：判定先交回等待方，再把这个执行节点记为阻塞并检查执行上限。</summary>
    private SettlePlan Block(RuntimeExecutable node, GraphExecution execution)
    {
        _task.NotifyExecutionSettled(execution.Unit);
        GraphExecutionSnapshot snapshot = execution.Snapshot();
        if (snapshot.StartedAt is null
            && snapshot.State == ExecutionState.Interrupted)
        {
            _task.AppendInfo($"{node.Path}：执行未启动即中断。");

            return new SettlePlan([], ExecutionInterruption.NotStarted);
        }

        string summary = snapshot.Failures.FirstOrDefault(failure => !string.IsNullOrWhiteSpace(failure))
            ?? snapshot.State switch
            {
                ExecutionState.Interrupted => "执行已中断。",
                _ => "执行未成功收口。",
            };
        _task.AppendError($"{node.Path}：{summary}");
        node.EnterBlocked([node.Path], new NodeBlock(summary, execution.Id, DateTimeOffset.UtcNow));
        NoteLimitReached(node);

        RestorePlan? restore = CreateRestorePlan(node);
        if (restore is not null)
        {
            _task.AppendInfo($"{node.Path} 的阻塞可恢复。");
        }

        return new SettlePlan(
            [],
            restore is not null ? ExecutionInterruption.Restorable : ExecutionInterruption.NonRestorable,
            restore);
    }

    /// <summary>失败停驻的路径已达执行上限时记录提示，后续重新执行不会放行。</summary>
    private void NoteLimitReached(RuntimeExecutable node)
    {
        if (LimitOf(node) is not { } limit || node.Executions < limit)
        {
            return;
        }

        _task.AppendError($"节点 {node.Path} 的执行次数已达上限 {limit} 次，不再重跑。");
    }

    /// <summary>产出发布后的共同收口：把待评估的祖先容器与依赖该节点的目标并入通知。</summary>
    private SettlePlan Settled(RuntimeExecutable node, IReadOnlyList<NodePath> downstream)
    {
        IReadOnlyList<NodePath> containers = AncestorContainers(node.Path);
        List<NodePath> notify = [.. downstream, .. containers, .. Graph.Dependents(node.Path)];
        InvalidateRestore(node.Path);
        InvalidateRestores(notify);
        _task.AppendInfo($"{node.Path} 执行成功。");

        return new SettlePlan(notify);
    }

    // ---- 推进评估 ----

    /// <summary>一个节点被激活后的评估：非派发型节点在其中自行处理并报告版本与失败，派发型的按就绪条件启动一次执行。
    /// 返回要通知的下游与要启动的执行。要求持有 Gate。</summary>
    public EvaluateResult Evaluate(NodeRuntime node)
    {
        if (Graph.ShapeOf(node.Path) is not { } shape)
        {
            return new EvaluateResult([]);
        }

        if (Graph.StaticBindings.Any(binding =>
            binding.Target == node.Path && !State(binding.Source).Released))
        {
            return new EvaluateResult([]);
        }

        if (!shape.DispatchesExecutions)
        {
            if (node.Canceled || node.AwaitingInput || node.Snapshot().State == NodeState.Blocked)
            {
                return new EvaluateResult([]);
            }

            InputProgress progress = node.Progress;

            NodeInputValues inputs = NodeInputValues.Empty;
            if (node.NeedsInput)
            {
                if (Graph.PlanOf(node.Path) is not { } plan)
                {
                    return new EvaluateResult([]);
                }

                if (plan.StaticOnly && progress.Executions == 0)
                {
                    inputs = NodeInputValues.Empty;
                }
                else
                {
                    // 不满足触发条件的组先消耗；消耗后没有其他可驱动组时不再激活节点
                    if (ConsumeSuppressed(node, progress, plan) && plan.DrivenGroup(progress, this) is null)
                    {
                        return new EvaluateResult([]);
                    }

                    if (!plan.Ready(progress, this))
                    {
                        return new EvaluateResult([]);
                    }

                    int? group = progress.ForceRerun && progress.LastInputGroup is { } last
                        ? last
                        : plan.DrivenGroup(progress, this) ?? plan.AvailableGroup(this);

                    if (group is not { } index || index < 0 || index >= plan.Groups.Count)
                    {
                        return new EvaluateResult([]);
                    }

                    inputs = NodeInputValues.From(plan.Groups[index], this);
                    progress.RecordGroup(index);
                }
            }

            if (node.Node is ExecutableNode declared)
            {
                inputs = WithStatic(declared, inputs);
            }

            long revision = node.Revision;
            node.Activate(this, inputs);

            if (Graph.PlanOf(node.Path) is { } consumed)
            {
                ConsumePlan(progress, consumed);
            }

            if (node.TakeFailure() is { Length: > 0 } failure)
            {
                _task.AppendError($"{node.Path}：{failure}");
                _task.Report(new ExecutionNotice(NoticeLevel.Warning, $"{_task.Id} 停在节点 {node.Path.Label}，{failure}"));
                _task.Touch();
            }

            if (node.Revision == revision)
            {
                return new EvaluateResult([]);
            }

            _task.Touch();

            return new EvaluateResult(Notify([node.Path]), ActivationKind.Published);
        }

        if (node is not RuntimeExecutable executable)
        {
            return new EvaluateResult([]);
        }

        if (!CanStart(executable))
        {
            if (Graph.PlanOf(executable.Path) is { } plan)
            {
                ConsumeSuppressed(executable, executable, plan);
            }

            return new EvaluateResult([]);
        }

        return new EvaluateResult([], ActivationKind.Dispatched, Start(executable));
    }

    /// <summary>登记方案内全部启动组的当前版本。</summary>
    private void ConsumePlan(InputProgress progress, NodeInputPlan plan)
    {
        foreach (InputGroup group in plan.Groups)
        {
            progress.Consume(group, this);
        }
    }

    /// <summary>消耗所有由新版本驱动但存在不满足触发条件绑定的启动组，每组写一条信息记录。节点在跑、阻塞、取消、强制重跑或达到执行上限时不消耗。</summary>
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

    /// <summary>记录一次抑制组消耗：给出节点、未满足触发条件的来源与端口。</summary>
    private void NoteSuppressed(NodeRuntime node, InputGroup group)
    {
        string ports = string.Join('、', group.Bindings
            .Where(binding => !binding.Admits(State(binding.Source)))
            .Select(SuppressedBinding));
        string message = $"{_task.Id} 的节点 {node.Path.Label} 有启动组未满足触发条件：{ports}，本组已消耗，不再等待。";

        _task.AppendInfo(message);
        _task.Report(new ExecutionNotice(NoticeLevel.Information, message));
        _task.Touch();
    }

    /// <summary>被抑制的绑定：来源端口、实际触发值与翻转条件。</summary>
    private string SuppressedBinding(InputBinding binding)
    {
        ISourceState source = State(binding.Source);
        bool fires = binding.SourcePort is { } port && source.Triggers(port);
        string condition = binding.Condition == TriggerCondition.Negative ? "，翻转条件要求 false" : string.Empty;

        return $"{binding.Source.Display} 的产出为 {(fires ? "true" : "false")}{condition}";
    }

    /// <summary>一次评估的结果：要通知的下游执行节点路径、激活方式，以及派发执行要用的输入。</summary>
    public readonly record struct EvaluateResult(
        IReadOnlyList<NodePath> Notify,
        ActivationKind Activation = ActivationKind.None,
        NodeInputValues? Inputs = null);

    /// <summary>一次评估的激活方式。</summary>
    public enum ActivationKind
    {
        /// <summary>没有激活。</summary>
        None,

        /// <summary>已派发一次执行，等待收口。</summary>
        Dispatched,

        /// <summary>已发布一次产出。</summary>
        Published,
    }

    /// <summary>任务启动的第一批：开始已放行，由它的直连下游、静态输入就绪后的执行节点与可评估的容器起步。
    /// 执行节点返回前全部实例化，容器只给路径由推进循环评估时实例化。恢复时已发布的运行期常量会让等待它的节点一并入队，动态来源未就绪的节点由启动判定挡住。</summary>
    public IReadOnlyList<NodePath> Roots() =>
        [.. Graph.StartOutgoing.Select(binding => _materializer.Get(binding.Target).Path),
            .. Graph.ExecutableNodes
                .Where(node => Graph.PlanOf(node.Path) is { StaticOnly: true })
                .Where(Startable)
                .Select(node => _materializer.Get(node.Path).Path),
            .. Graph.ExecutableNodes
                .Where(node => Graph.PlanOf(node.Path) is { } plan
                    && plan.StaticBindings.Count > 0
                    && plan.StaticBindings.All(binding => State(binding.Source).Released))
                .Select(node => _materializer.Get(node.Path).Path),
            .. Graph.Containers
                .Where(ContainerNeedsEvaluation)
                .Select(container => container.Path)];


    /// <summary>一个重新执行目标：停住的节点，与它要重跑的节点。执行失败时目标为节点自身。</summary>
    public readonly record struct RerunTarget(NodePath Blocked, NodePath Node);

    /// <summary>计算重新执行会作废并重跑的目标：各停在阻塞的执行节点记录的目标，按执行节点表顺序。不改变状态。要求持有 Gate。</summary>
    public IReadOnlyList<RerunTarget> PlanReruns()
    {
        List<RerunTarget> targets = [];
        foreach (RuntimeExecutable node in MaterializedExecutables())
        {
            foreach (NodePath target in node.RerunTargets)
            {
                if (AtLimit(Executable(target)) || Frozen(target))
                {
                    continue;
                }

                targets.Add(new RerunTarget(node.Path, target));
            }
        }

        return targets;
    }

    /// <summary>一个可执行的恢复计划：停住的节点、提供恢复数据的来源执行与已装配的恢复。</summary>
    public sealed record RestorePlan(
        NodePath Blocked,
        GraphExecution Source,
        GraphExecutionInput Input,
        PreparedExecutionRestore Prepared);

    /// <summary>是否存在可派发的恢复计划。要求持有 Gate。</summary>
    public bool HasRestorePlans => _restorePlans.Count > 0;

    /// <summary>取出当前恢复计划，按执行节点表顺序。不改变状态。要求持有 Gate。</summary>
    public IReadOnlyList<RestorePlan> PlanRestores() =>
        [.. MaterializedExecutables()
            .Select(node => _restorePlans.GetValueOrDefault(node.Path))
            .OfType<RestorePlan>()];

    /// <summary>阻塞节点此刻是否已有恢复计划。要求持有 Gate。</summary>
    public bool CanRestore(NodePath path) => _restorePlans.ContainsKey(path);

    /// <summary>从存档恢复任务后按当前阻塞与来源执行重建全部恢复计划。要求持有 Gate。</summary>
    public void RefreshRestorePlans()
    {
        ClearRestorePlans();
        foreach (RuntimeExecutable node in MaterializedExecutables())
        {
            _ = CreateRestorePlan(node);
        }
    }

    /// <summary>清除全部恢复计划。任务完成与终止时调用。</summary>
    public void ClearRestorePlans()
    {
        foreach (RuntimeExecutable node in MaterializedExecutables())
        {
            node.HasRestorePlan = false;
        }

        _restorePlans.Clear();
    }

    /// <summary>给定计划是否仍是该节点当前的恢复计划。要求持有 Gate。</summary>
    internal bool IsCurrentRestorePlan(RestorePlan plan) =>
        _restorePlans.TryGetValue(plan.Blocked, out RestorePlan? current)
        && ReferenceEquals(current, plan);

    /// <summary>恢复计划已经派发，移除缓存。要求持有 Gate。</summary>
    internal void RemoveRestorePlan(RestorePlan plan)
    {
        if (IsCurrentRestorePlan(plan) && Materialized(plan.Blocked) is RuntimeExecutable node)
        {
            SetRestorePlan(node, null);
        }
    }

    /// <summary>让这些目标的当前阻塞不再关联可恢复的执行并丢弃恢复计划。上游产出变化与重新执行时调用；恢复数据作为历史保留。</summary>
    public void InvalidateRestores(IReadOnlyList<NodePath> nodes)
    {
        foreach (NodePath path in nodes)
        {
            InvalidateRestore(path);
        }
    }

    /// <summary>让该节点的当前阻塞不再关联可恢复的执行并丢弃恢复计划；恢复数据作为历史保留。</summary>
    public void InvalidateRestore(NodePath path)
    {
        if (Materialized(path) is RuntimeExecutable node)
        {
            SetRestorePlan(node, null);
            node.InvalidateRestore();
        }
    }

    /// <summary>按阻塞执行与恢复来源建立一次恢复计划，调用节点类型校验并缓存结果。</summary>
    private RestorePlan? CreateRestorePlan(RuntimeExecutable node)
    {
        SetRestorePlan(node, null);

        if (_task.Completed
            || _task.Lifecycle == TaskLifecycle.Terminated
            || node.Canceled
            || node.Block?.Execution is not { } blocked
            || AtLimit(node)
            || Frozen(node.Path))
        {
            return null;
        }

        GraphExecution? candidate = _state.Executions.FirstOrDefault(execution => execution.Id == blocked);
        while (candidate is not null)
        {
            if (!Resumable(candidate))
            {
                return null;
            }

            if (candidate.RestoreData is { } data)
            {
                GraphExecutionInput input = candidate.Input with
                {
                    ExecutionCount = node.Executions + 1,
                    OutputPorts = node.Executable.SubmittablePorts,
                };

                ExecutionRequest request = GraphExecution.BuildRequest(input);
                ExecutionRestoreContext context = new(
                    candidate.Id,
                    request,
                    GraphExecution.BuildTarget(input, request),
                    data,
                    GraphExecution.ToNeutralContract(candidate.Contract));
                ErrorOr<PreparedExecutionRestore> prepared;
                try
                {
                    prepared = _executions.PrepareRestore(context);
                }
                catch (Exception)
                {
                    return null;
                }

                if (prepared.IsError)
                {
                    return null;
                }

                RestorePlan plan = new(node.Path, candidate, input, prepared.Value);
                SetRestorePlan(node, plan);

                return plan;
            }

            // 已经消耗过请求但没有留下恢复数据的尝试不能回退到更早的状态，否则会从更早历史重放请求与工具调用
            if (candidate.Snapshot().Usage.RequestCount > 0)
            {
                return null;
            }

            candidate = candidate.ContinuedFrom is { } previous
                ? _state.Executions.FirstOrDefault(execution => execution.Id == previous)
                : null;
        }

        return null;
    }

    /// <summary>可从这次执行继续：失败与中断可以恢复，完成不恢复。</summary>
    private static bool Resumable(GraphExecution execution) =>
        execution.State is ExecutionState.Failed or ExecutionState.Interrupted;

    /// <summary>缓存或移除节点的恢复计划，并同步快照标记。</summary>
    private void SetRestorePlan(RuntimeExecutable node, RestorePlan? plan)
    {
        if (plan is null)
        {
            _restorePlans.Remove(node.Path);
        }
        else
        {
            _restorePlans[node.Path] = plan;
        }

        node.HasRestorePlan = plan is not null;
    }

    /// <summary>节点是否已经产出过内容。</summary>
    private bool HasPublished(NodePath node) => Materialized(node) is { Revision: > 0 };

    /// <summary>已发布的 Once 节点冻结，不再重新执行或作废。</summary>
    private bool Frozen(NodePath node) =>
        HasPublished(node) && Graph[node] is ExecutableNode { Spec.Emission: NodeEmission.Once };

    /// <summary>解除一个执行节点的阻塞：移除本次重新执行处理的目标，剩余目标保持阻塞，全部处理后解除阻塞。</summary>
    public void Unblock(NodePath node, IReadOnlyList<NodePath> handled)
    {
        if (Materialized(node) is RuntimeExecutable executable)
        {
            executable.FinishRerun(handled);
        }
    }

    /// <summary>未实例化节点的来源状态：没有产出、版本为零。</summary>
    private sealed class UnmaterializedSource : ISourceState
    {
        public bool Released => false;

        public long Revision => 0;
    }

    /// <summary>未实例化节点的进展：没有已消费版本、没有执行次数、没有产出。</summary>
    private sealed class UnmaterializedProgress : IInputProgress
    {
        public bool ForceRerun => false;

        public bool Published => false;

        public long ConsumedRevision(ConsumptionKey key) => 0;

        public void Consume(InputGroup group, IReadinessSources sources)
        {
        }

        public int Executions => 0;
    }
}