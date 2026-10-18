using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Node.Input;

/// <summary>输入节点在任务内的运行期对象：不启动 run，在自己的执行里声明接收外部输入，交付即产出，取消后不再接收。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class InputRuntime(ExecutableNode executable, IInputChannel channel, INodeContext context)
    : NodeRuntime(executable)
{
    private string? _answer;
    private bool _canceled;
    private long _revision;

    /// <summary>产出已放行、可被下游消费：已有回答且未被取消。</summary>
    public override bool Released => !_canceled && _answer is not null;

    /// <summary>放行代数：每次回答递增，是版本驱动的依据。</summary>
    public override long Revision => _revision;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>随任务进入取消态并撤销接收声明。</summary>
    public override void Cancel()
    {
        _canceled = true;
        channel.Decline(context, Path);
    }

    /// <summary>被激活时清掉上一轮回答并声明接收外部输入，取消后不再声明。</summary>
    public override void Activate(INodeContext context)
    {
        if (_canceled)
        {
            return;
        }

        _answer = null;
        channel.Accept(context, Path, Deliver);
    }

    /// <summary>收下一次外部输入：记下回答并推进放行代数，声明由交付方随交付撤销。</summary>
    private void Deliver(string text)
    {
        _answer = text;
        _revision++;
    }

    /// <summary>按输出端口取回答文本，端口不是 Text 或尚未回答时为空。</summary>
    public override string? PortText(INodeTask task, PortName port) =>
        executable.Outputs.Find(port) is { Type: PortType.Text } && _answer is { Length: > 0 } answer ? answer : null;

    /// <summary>按输出端口取可注入的回答消息，出处指向用户输入。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(INodeTask task, PortName port, int? item) =>
        item is null && Released && executable.Outputs.Find(port) is { Type: PortType.Text } && _answer is { Length: > 0 } answer
            ? [new ContextMessage(MessageRole.User, $"节点「{Path}」的输入：\n{answer}", new InputSource(Path))]
            : [];

    /// <summary>输入节点在快照里的一刻状态。</summary>
    public override ExecutableStateSnapshot Snapshot()
    {
        NodeState state = NodeState.Pending;
        if (_canceled)
        {
            state = NodeState.Canceled;
        }
        else if (Released)
        {
            state = NodeState.Done;
        }

        return new ExecutableStateSnapshot(Path, state, [], 0, []);
    }
}
