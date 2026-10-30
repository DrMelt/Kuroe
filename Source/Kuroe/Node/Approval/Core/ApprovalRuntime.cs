using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Approval;

/// <summary>审批节点在任务内的运行期对象：输入齐备即停在待批准，在自己的执行里声明接收外部输入，交付即批准并按输入端口放出停驻时的取值。
/// 不启动执行、不承载自身内容，取消后不再接收。读写都要求持有任务 Gate。</summary>
internal sealed class ApprovalRuntime(
    ExecutableNode executable,
    IInputChannel channel,
    INodeContext nodeContext,
    IConsumedRevisionStore? consumed = null)
    : RuntimeNode(executable), IInputProgress
{
    /// <summary>停驻时装入的输入，放行后按同名输出端口放出。</summary>
    private NodeInputValues _inputs = NodeInputValues.Empty;

    private readonly IConsumedRevisionStore _consumed = consumed ?? new ConsumedRevisionStore();
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
        channel.Decline(nodeContext, Path);
    }

    long IInputProgress.ConsumedRevision(ConsumptionKey key) => _consumed.Revision(key);

    void IInputProgress.Consume(InputGroup group, IReadinessSources sources) => _consumed.Record(group, sources);

    int IInputProgress.Executions => 0;

    bool IInputProgress.ForceRerun => false;

    /// <summary>装入本次输入并声明接收外部输入：停驻时保存输入端口取值，批准后按同名输出端口放出。已停驻或已取消时不再停驻。</summary>
    public override void Activate(INodeContext context, NodeInputValues inputs)
    {
        if (_canceled || _awaiting)
        {
            return;
        }

        _inputs = inputs;
        _awaiting = true;
        channel.Accept(context, Path, _ => Approve());
    }

    /// <summary>收下一次外部输入即批准：撤停驻并推进放行代数，下游据此按新版本重新评估。批准不带内容，声明由交付方随交付撤销。</summary>
    private void Approve()
    {
        _awaiting = false;
        _revision++;
    }

    /// <summary>按输出端口放出停驻时装入的输入文本：输入端口与输出端口同名，未放行时为空。</summary>
    public override string? PortText(INodeContext context, PortName port) =>
        Released ? _inputs.Text(port) : null;

    /// <summary>按输出端口放出停驻时装入的输入取值。审批节点一次放行全部端口。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Released ? _inputs.Values(port) : [];

    /// <summary>按输出端口放出停驻时记录的触发值，未放行时按触发处理。</summary>
    public override bool Triggers(PortName port) => !Released || _inputs.Triggers(port);

    /// <summary>捕获停驻输入、待批准状态与放行代数。</summary>
    public override NodeCheckpoint? CaptureCheckpoint() =>
        new ApprovalNodeCheckpoint(
            Path,
            new NodeCommonCheckpoint(Revision, LastInputGroup, _consumed.CaptureCheckpoint(), [], false, false),
            new NodeInputValuesCheckpoint(
                [
                    .. _inputs.Ordered.Select(item => new NodeInputValueCheckpoint(
                        item.TargetInput,
                        item.Type.Name,
                        new InputValueCheckpoint(
                            item.TargetInput,
                            CheckpointSources.Write(item.Value.Origin),
                            item.Value.Text))),
                ],
                [.. _inputs.Texts.Keys],
                _inputs.Texts.Keys.ToDictionary(port => port, port => _inputs.Triggers(port)),
                new Dictionary<PortName, string>(_inputs.StaticTexts.Texts)),
            _awaiting);

    /// <summary>恢复停驻输入与待批准状态，待批准节点重新声明接收外部输入。</summary>
    public override void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context)
    {
        if (checkpoint is not ApprovalNodeCheckpoint state)
        {
            throw new ArgumentException($"审批节点 {Path} 收到 {checkpoint.GetType().Name} 检查点。", nameof(checkpoint));
        }

        RestoreLastInputGroup(state.Common.LastInputGroup);
        _consumed.RestoreCheckpoint(state.Common.Consumed);
        _revision = state.Common.Revision;
        _awaiting = state.Awaiting;
        _inputs = ReadInputs(state.Inputs);
        if (_awaiting && !_canceled)
        {
            channel.Accept(nodeContext, Path, _ => Approve());
        }
    }

    /// <summary>从检查点重建停驻输入。</summary>
    private NodeInputValues ReadInputs(NodeInputValuesCheckpoint state)
    {
        List<NodeInputValue> ordered = [];
        foreach (NodeInputValueCheckpoint item in state.Ordered)
        {
            PortType type = executable.FindInput(item.Target)?.Type ?? PortTypes.Text;
            ordered.Add(new NodeInputValue(
                item.Target,
                type,
                new ContextValue(CheckpointSources.Read(item.Value.Origin), item.Value.Text)));
        }

        return new NodeInputValues(
            ordered,
            state.Connected.ToHashSet(),
            state.Triggers,
            new PortTexts(state.StaticTexts));
    }

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
