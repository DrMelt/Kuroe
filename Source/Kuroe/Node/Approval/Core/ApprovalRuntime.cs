using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Node.Approval;

/// <summary>审批节点在任务内的运行期对象：启动条件齐备即停在待批准，在自己的执行里声明接收外部输入，交付即批准并按输入端口转发来源产出。
/// 不启动执行、不承载自身内容，取消后不再接收。读写都要求持有任务 Gate。</summary>
internal sealed class ApprovalRuntime(ExecutableNode executable, IInputChannel channel, INodeContext context)
    : NodeRuntime(executable), IInputProgress
{
    /// <summary>停在待批准时解析出的内容端口到来源端口的映射，未停驻时为空。信号输入不承载内容，不在此表中。</summary>
    private readonly Dictionary<PortName, (NodePath Source, PortName Port)> _content = [];

    private readonly ConsumptionLedger _consumed = new();
    private long _revision;
    private bool _awaiting;
    private bool _canceled;

    /// <summary>产出已放行、可被下游消费：已批准且不在待批准与取消。</summary>
    public override bool Released => !_canceled && !_awaiting && _revision > 0;

    /// <summary>放行代数：每次批准递增，是版本驱动的依据。作废不回退。</summary>
    public override long Revision => _revision;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>随任务进入取消态并撤销接收声明。</summary>
    public override void Cancel()
    {
        _canceled = true;
        channel.Decline(context, Path);
    }

    long IInputProgress.ConsumedRevision(NodePath source) => _consumed.Revision(source);

    int IInputProgress.Executions => 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>哪一组就绪就按哪一组取值：记下该组的来源端口并登记消费账，随后声明接收外部输入，重复停驻与取消后不停驻。</summary>
    public override void Activate(INodeContext context)
    {
        if (_canceled || _awaiting || context.Plan(Path) is not { } plan
            || NodeReadiness.AvailableGroup(plan, context) is not { } group)
        {
            return;
        }

        _content.Clear();
        foreach (FlowEdge edge in context.Graph.Incoming(Path).Where(edge => edge.Group == group))
        {
            if (edge.Type != PortType.Signal)
            {
                _content[edge.TargetInput] = (edge.Source, edge.SourcePort);
            }
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
    public override string? PortText(INodeTask task, PortName port) =>
        Released && _content.TryGetValue(port, out (NodePath Source, PortName Port) reference)
            ? context.Runtime(reference.Source).PortText(task, reference.Port)
            : null;

    /// <summary>按输出端口取产出：转发来源端口的取值。审批节点一次放行全部端口。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeTask task, PortName port) =>
        Released && _content.TryGetValue(port, out (NodePath Source, PortName Port) reference)
            ? context.Runtime(reference.Source).OutputValues(task, reference.Port)
            : [];

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
