using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Flow.Node.Shared.RuntimeGraph;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.Runtime.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;
using Kuroe.Tasks.Shared;

namespace Kuroe.Node.Input;

/// <summary>输入节点在任务内的运行期对象：不启动执行，在自己的执行里声明接收外部输入并给出提问，交付即产出，取消后不再接收。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class InputRuntime(ExecutableNode executable, IInputChannel channel, string? question, INodeContext nodeContext)
    : RuntimeNode(executable)
{
    private bool _canceled;
    private bool _waiting;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>回答产出可消费：未被取消。</summary>
    protected override bool Consumable => !_canceled;

    /// <summary>已声明接收外部输入且尚未交付。</summary>
    public override bool AwaitingInput => _waiting;

    /// <summary>输入节点等待外部交付，不依赖来源取值。</summary>
    public override bool NeedsInput => false;

    /// <summary>随任务进入取消态并撤销接收声明。</summary>
    public override void Cancel()
    {
        _canceled = true;
        _waiting = false;
        channel.Decline(nodeContext, Path);
    }

    /// <summary>被激活时清掉上一轮回答并声明接收外部输入，取消后不再声明。输入节点不消费来源取值。</summary>
    public override void Activate(INodeContext context, INodeInputs inputs)
    {
        if (_canceled)
        {
            return;
        }

        _waiting = true;
        Progress.Invalidate();
        channel.Accept(context, Path, question, Deliver);
    }

    /// <summary>收下一次外部输入：按回答发布一次，声明由交付方随交付撤销。</summary>
    private void Deliver(string text)
    {
        _waiting = false;
        Progress.Started();
        Progress.Publish(CreatePublication(Progress.NextRevision(), text));
    }

    /// <summary>捕获等待状态、进展与发布。</summary>
    public override NodeRecord? CaptureRecord() =>
        new InputNodeRecord(
            Path,
            CaptureProgress(),
            _waiting);

    /// <summary>恢复等待状态、进展与发布，等待中的节点重新声明接收外部输入。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
        if (record is not InputNodeRecord state)
        {
            throw new ArgumentException($"输入节点 {Path} 收到 {record.GetType().Name} 记录。", nameof(record));
        }

        RestoreProgress(state.Common);
        _waiting = state.Waiting;

        if (_waiting && !_canceled)
        {
            channel.Accept(nodeContext, Path, question, Deliver);
        }
    }

    /// <summary>输入节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state = NodeState.Pending;
        if (_canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Progress.Published)
        {
            state = NodeState.Done;
        }

        return new ExecutableStateSnapshot(Path, state);
    }

    /// <summary>按回答构造一次发布：Text 语义端口给出同一条回答，出处指向本节点。</summary>
    private Publication CreatePublication(long revision, string answer)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = answer is { Length: > 0 } text
            ? executable.Outputs
                .Where(port => PortRules.CarriesText(port.Type))
                .ToDictionary(
                    port => port.Name,
                    port => (IReadOnlyList<ContextValue>)[new ContextValue(new InputSource(Path, port.Name), text)])
            : [];

        return new Publication(revision, new Dictionary<PortName, bool>(), content);
    }
}
