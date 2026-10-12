using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows.Flows;
using StartNode = Kuroe.Shared.Workflows.Graph.StartNode;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>开始节点在任务内的运行时对象：任务启动即已放行，产出是提交时给的初始输入，带固定值的端口给配置里的值。
/// 版本恒为 1，下游只消费一次。不执行 run、不等待、不取消。</summary>
internal sealed class RuntimeStart(StartNode node, string goal) : RuntimeNode(node)
{
    /// <summary>提交时给出的初始输入，开始目标端口的产出。</summary>
    private readonly string _goal = goal;

    public override bool Released => true;

    public override long Revision => 1;

    public override bool Awaiting => false;

    public override bool Canceled => false;

    /// <summary>按端口取初始输入：带固定值的端口给该值，其余 Text 端口给提交的目标，未声明端口无产出。</summary>
    public override string? PortText(WorkTask task, PortName port) => Text(port);

    /// <summary>按端口取初始输入消息，出处指向流程起点。</summary>
    public override IReadOnlyList<ContextMessage> OutputMessages(WorkTask task, PortName port, int? item) =>
        Text(port) is { Length: > 0 } text
            ? [new ContextMessage(MessageRole.User, text, new StartSource(port))]
            : [];

    public override void Cancel()
    {
    }

    private string? Text(PortName port) =>
        node.FindPort(port) is { Type: PortType.Text } declared ? declared.Value ?? _goal : null;
}
