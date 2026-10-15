using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Approval;

/// <summary>审批节点在任务内的运行期对象：启动条件齐备即停在待批准，批准后按输入端口转发来源产出。
/// 不启动 run、不承载自身内容。读写都要求持有任务 Gate。</summary>
internal sealed class ApprovalRuntime(ExecutableNode executable, INodeContext context)
    : NodeRuntime(executable), IInputProgress, IWaitNode
{
    /// <summary>停在待批准时解析出的内容端口到来源端口的映射，未停驻时为空。信号输入不承载内容，不在此表中。</summary>
    private readonly Dictionary<PortName, (int Source, PortName Port)> _content = [];

    private readonly ConsumptionLedger _consumed = new();
    private long _revision;
    private bool _awaiting;
    private bool _canceled;

    /// <summary>产出已放行、可被下游消费：已批准且不在待批准与取消。</summary>
    public override bool Released => !_canceled && !_awaiting && _revision > 0;

    /// <summary>放行代数：每次批准递增，是版本驱动的依据。作废不回退。</summary>
    public override long Revision => _revision;

    /// <summary>等待种类：审批节点等用户批准。</summary>
    public AwaitKind Kind => AwaitKind.Approve;

    /// <summary>停在待批准。</summary>
    public bool Awaiting => _awaiting;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    IReadOnlyList<int>? IInputProgress.AdoptedItems => null;

    long IInputProgress.ConsumedRevision(int source) => _consumed.Revision(source);

    bool IInputProgress.ConsumedOnce(int source) => _consumed.ConsumedOnce(source);

    bool IInputProgress.Produced(int? item) => item is null && Released;

    int IInputProgress.Runs(int? item) => 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>哪一组就绪就按哪一组取值：记下该组的来源端口并登记消费账，重复停驻与取消后不停驻。</summary>
    public override void Activate(INodeContext context)
    {
        if (_canceled || _awaiting || context.Plan(Index) is not { } plan
            || NodeReadiness.AvailableGroup(plan, null, context) is not { } group)
        {
            return;
        }

        _content.Clear();
        foreach (FlowEdge edge in context.Graph.Incoming(Index).Where(edge => edge.Group == group))
        {
            if (edge.Type != PortType.Signal)
            {
                _content[edge.TargetInput] = (edge.Source, edge.SourcePort);
            }
        }

        _consumed.Record(plan, context);
        _awaiting = true;
    }

    /// <summary>外部输入即批准：撤待批准并推进放行代数，下游据此按新版本重新评估。</summary>
    public void Receive(string text)
    {
        if (!_awaiting)
        {
            return;
        }

        _awaiting = false;
        _revision++;
    }

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>按输出端口转发来源产出：放行时取当时齐备组解析出的来源端口内容，否则为空。</summary>
    public override string? PortText(INodeTask task, PortName port) =>
        Released && _content.TryGetValue(port, out (int Source, PortName Port) reference)
            ? context.Runtime(reference.Source).PortText(task, reference.Port)
            : null;

    /// <summary>按输出端口取可注入的产出消息：转发来源端口的消息集。审批节点整节点放行，不按条目取值。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(INodeTask task, PortName port, int? item) =>
        Released && item is null && _content.TryGetValue(port, out (int Source, PortName Port) reference)
            ? context.Runtime(reference.Source).OutputMessages(task, reference.Port, item)
            : [];

    /// <summary>审批节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot() => new(Index, State, [], 0, []);

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
