using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Node.Shared.RuntimeGraph;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.Runtime.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Approval;

/// <summary>审批节点在任务内的运行期对象：输入齐备即停在待批准，在自己的执行里声明接收外部输入并给出提问，交付即批准并按输入端口放出停驻时的取值。
/// 不启动执行、不承载自身内容，取消后不再接收。读写都要求持有任务 Gate。</summary>
internal sealed class ApprovalRuntime(
    ExecutableNode executable,
    IInputChannel channel,
    string? question,
    INodeContext nodeContext)
    : RuntimeNode(executable)
{
    /// <summary>停驻时装入的输入内容，放行后按同名输出端口放出。</summary>
    private INodeInputs _inputs = NodeInputValues.Empty;

    private bool _awaiting;
    private bool _canceled;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>已批准产出可消费：不在待批准与取消。</summary>
    protected override bool Consumable => !_canceled && !_awaiting;

    /// <summary>已停在待批准，等待外部批准。</summary>
    public override bool AwaitingInput => _awaiting;

    /// <summary>随任务进入取消态并撤销接收声明。</summary>
    public override void Cancel()
    {
        _canceled = true;
        _awaiting = false;
        channel.Decline(nodeContext, Path);
    }

    /// <summary>装入本次输入并声明接收外部输入：停驻时保存输入端口取值，批准后按同名输出端口放出。已停驻或已取消时不再停驻。</summary>
    public override void Activate(INodeContext context, INodeInputs inputs)
    {
        if (_canceled || _awaiting)
        {
            return;
        }

        _inputs = inputs;
        _awaiting = true;
        channel.Accept(context, Path, question, _ => Approve());
    }

    /// <summary>收下一次外部输入即批准：撤停驻并发布一次，下游据此按新版本重新评估。批准不带内容，声明由交付方随交付撤销。</summary>
    private void Approve()
    {
        _awaiting = false;
        Progress.Started();
        Progress.Publish(CreatePublication(Progress.NextRevision()));
    }

    /// <summary>按停驻输入构造一次发布：输出端口取自身声明与已连接输入端口，内容取该端口的停驻取值。
    /// 批准事件本身放行，发布不携带输入的触发位。</summary>
    private Publication CreatePublication(long revision)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = [];
        IEnumerable<PortName> ports = executable.Outputs
            .Select(port => port.Name)
            .Concat(_inputs.Ordered.Select(item => item.TargetInput))
            .Distinct();
        foreach (PortName port in ports)
        {
            content[port] = _inputs.Values(port);
        }

        return new Publication(revision, new Dictionary<PortName, bool>(), content);
    }

    /// <summary>捕获停驻输入内容、待批准状态、进展与发布。</summary>
    public override NodeRecord? CaptureRecord() =>
        new ApprovalNodeRecord(
            Path,
            CaptureProgress(),
            new NodeInputValuesRecord(
                [
                    .. _inputs.Ordered.Select(item => new NodeInputValueRecord(
                        item.TargetInput,
                        item.Type.Name,
                        new InputValueRecord(
                            item.TargetInput,
                            RecordSources.Write(item.Value.Origin),
                            item.Value.Text))),
                ]),
            _awaiting);

    /// <summary>恢复停驻输入与待批准状态，待批准节点重新声明接收外部输入。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
        if (record is not ApprovalNodeRecord state)
        {
            throw new ArgumentException($"审批节点 {Path} 收到 {record.GetType().Name} 记录。", nameof(record));
        }

        RestoreProgress(state.Common);
        _awaiting = state.Awaiting;
        _inputs = ReadInputs(state.Inputs);

        if (_awaiting && !_canceled)
        {
            channel.Accept(nodeContext, Path, question, _ => Approve());
        }
    }

    /// <summary>从存档重建停驻输入内容。</summary>
    private NodeInputValues ReadInputs(NodeInputValuesRecord state)
    {
        List<NodeInputValue> ordered = [];
        foreach (NodeInputValueRecord item in state.Ordered)
        {
            PortType type = executable.FindInput(item.Target)?.Type ?? PortTypes.Text;
            ordered.Add(new NodeInputValue(
                item.Target,
                type,
                new ContextValue(RecordSources.Read(item.Value.Origin), item.Value.Text)));
        }

        return new NodeInputValues(ordered, new HashSet<PortName>());
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
