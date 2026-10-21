using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using StartNode = Kuroe.Flow.Shared.StaticGraph.Graph.StartNode;

namespace Kuroe.Flow.RuntimeNodes;

/// <summary>开始节点在任务内的运行时对象：任务启动即已放行，产出是提交时给的初始输入，带固定值的端口给配置里的值。
/// 版本恒为 1，下游只消费一次。不派发执行、不等待、不取消。</summary>
internal sealed class RuntimeStart(StartNode node, string goal) : NodeRuntime(node)
{
    /// <summary>提交时给出的初始输入，开始目标端口的产出。</summary>
    private readonly string _goal = goal;

    public override bool Released => true;

    public override long Revision => 1;

    public override bool Canceled => false;

    /// <summary>按端口取初始输入：带固定值的端口给该值，其余 Text 端口给提交的目标，未声明端口无产出。</summary>
    public override string? PortText(INodeTask task, PortName port) => Text(port);

    /// <summary>按端口取初始输入消息，出处指向流程起点。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeTask task, PortName port) =>
        Text(port) is { Length: > 0 } text
            ? [new ContextValue(new StartSource(port), text)]
            : [];

    public override void Cancel()
    {
    }

    private string? Text(PortName port) =>
        node.FindPort(port) is { Type: PortType.Text } declared ? declared.Value ?? _goal : null;
}
