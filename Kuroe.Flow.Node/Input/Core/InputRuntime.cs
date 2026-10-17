using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.ExecutableNode;

namespace Kuroe.Flow.Node.Input;

/// <summary>输入节点在任务内的运行期对象：不启动 run，停在等待时由外部回答放行，回答即产出。
/// 读写都要求持有任务 Gate。</summary>
internal sealed class InputRuntime(ExecutableNode executable) : NodeRuntime(executable), IWaitNode
{
    private string? _answer;
    private bool _awaiting;
    private bool _canceled;
    private long _revision;

    /// <summary>产出已放行、可被下游消费：已有回答且未被取消。</summary>
    public override bool Released => !_canceled && _answer is not null;

    /// <summary>放行代数：每次回答递增，是版本驱动的依据。</summary>
    public override long Revision => _revision;

    /// <summary>随任务取消。</summary>
    public override bool Canceled => _canceled;

    /// <summary>随任务进入取消态。</summary>
    public override void Cancel() => _canceled = true;

    /// <summary>等待种类：等用户回答。</summary>
    public AwaitKind Kind => AwaitKind.Answer;

    /// <summary>停在等待用户回答。</summary>
    public bool Awaiting => _awaiting;

    /// <summary>被激活时清掉上一轮回答并重新挂起等待回答。</summary>
    public override void Activate(INodeContext context)
    {
        _answer = null;
        _awaiting = true;
    }

    /// <summary>交付一次外部输入：撤等待并记下回答，放行代数随之推进。</summary>
    public void Receive(string text)
    {
        if (!_awaiting)
        {
            return;
        }

        _awaiting = false;
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
