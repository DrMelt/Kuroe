using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Approval;

/// <summary>审批节点在任务内的运行期对象：启动条件齐备即停在待批准，在自己的执行里声明接收外部输入，交付即批准并按输入端口转发来源产出。
/// 不启动执行、不承载自身内容，取消后不再接收。读写都要求持有任务 Gate。</summary>
internal sealed class ApprovalRuntime(
    ExecutableNode executable,
    IInputChannel channel,
    INodeContext context,
    IConsumptionLedger? consumed = null)
    : NodeRuntime(executable), IInputProgress
{
    /// <summary>停驻时解析出的端口到来源端口的映射，放行后按它转发产出与触发值。</summary>
    private readonly Dictionary<PortName, (NodePath Source, PortName Port)> _sources = [];

    private readonly IConsumptionLedger _consumed = consumed ?? new ConsumptionLedger();
    private long _revision;
    private bool _awaiting;
    private bool _canceled;

    /// <summary>产出已放行、可被下游消费：已批准且不在待批准与取消。</summary>
    public override bool Released => !_canceled && !_awaiting && _revision > 0;

    /// <summary>放行代数：每次批准递增，是版本驱动的依据。作废不回退。</summary>
    public override long Revision => _revision;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>已停在待批准，等待外部批准。</summary>
    public override bool AwaitingInput => _awaiting;

    /// <summary>随任务进入取消态并撤销接收声明。</summary>
    public override void Cancel()
    {
        _canceled = true;
        _awaiting = false;
        channel.Decline(context, Path);
    }

    long IInputProgress.ConsumedRevision(ConsumptionKey key) => _consumed.Revision(key);

    void IInputProgress.Consume(InputGroup group, IReadinessSources sources) => _consumed.Record(group, sources);

    int IInputProgress.Executions => 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>哪一组由新版本驱动就按哪一组取值：记下该组的来源端口，登记全部启动组的当前版本，随后声明接收外部输入。已停驻或已取消时不再停驻。</summary>
    public override void Activate(INodeContext context)
    {
        if (_canceled || _awaiting || context.Plan(Path) is not { } plan
            || plan.DrivenGroup(this, context) is not { } group)
        {
            return;
        }

        _sources.Clear();
        foreach (FlowEdge edge in context.Graph.Incoming(Path).Where(edge => edge.Group == group))
        {
            _sources[edge.TargetInput] = (edge.Source, edge.SourcePort);
        }

        _consumed.Record(plan, context);
        _awaiting = true;
        channel.Accept(context, Path, _ => Approve());
    }

    /// <summary>收下一次外部输入即批准：撤停驻并推进放行代数，下游据此按新版本重新评估。批准不带内容，声明由交付方随交付撤销。</summary>
    private void Approve()
    {
        _awaiting = false;
        _revision++;
    }

    /// <summary>按输出端口转发来源产出：放行时取当时齐备组解析出的来源端口内容，否则为空。</summary>
    public override string? PortText(INodeContext context, PortName port) =>
        Released && _sources.TryGetValue(port, out (NodePath Source, PortName Port) reference)
            ? context.Runtime(reference.Source).PortText(context, reference.Port)
            : null;

    /// <summary>按输出端口取产出：转发来源端口的取值。审批节点一次放行全部端口。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Released && _sources.TryGetValue(port, out (NodePath Source, PortName Port) reference)
            ? context.Runtime(reference.Source).OutputValues(context, reference.Port)
            : [];

    /// <summary>按输出端口转发来源端口的触发值。</summary>
    public override bool Triggers(PortName port) =>
        !Released
        || !_sources.TryGetValue(port, out (NodePath Source, PortName Port) reference)
        || context.Runtime(reference.Source).Triggers(reference.Port);

    /// <summary>审批节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot() => new(Path, State);

    private NodeState State
    {
        get
        {
            if (Canceled)
            {
                return NodeState.Canceled;
            }

            return Released ? NodeState.Done : NodeState.Pending;
        }
    }
}
