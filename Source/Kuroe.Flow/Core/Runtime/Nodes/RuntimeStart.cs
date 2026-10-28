using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>开始节点在任务内的运行时对象：任务启动即已放行，产出是提交时按端口给出的输入，带固定值的端口给配置里的值，
/// 绑定到宿主输入名的端口给任务启动时注入的值。版本恒为 1，下游只消费一次。不派发执行、不等待、不取消。</summary>
internal sealed class RuntimeStart(StartNode node, StartInputs inputs, IPortTexts startTexts) : NodeRuntime(node)
{
    /// <summary>提交时按端口给出的输入，不带固定值的端口的产出。</summary>
    private readonly StartInputs _inputs = inputs;

    /// <summary>任务启动时注入的开始端口文本，绑定到宿主输入名的端口的产出。</summary>
    private readonly IPortTexts _startTexts = startTexts;

    public override bool Released => true;

    public override long Revision => 1;

    public override bool Canceled => false;

    /// <summary>按端口取输入：带固定值的端口给该值，绑定到宿主输入名的端口给注入值，其余 Text 端口给提交的输入，未声明端口无产出。</summary>
    public override string? PortText(INodeContext context, PortName port) => Text(port);

    /// <summary>按端口取初始输入消息，出处指向流程起点。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Text(port) is { Length: > 0 } text
            ? [new ContextValue(new StartSource(port), text)]
            : [];

    public override void Cancel()
    {
    }

    private string? Text(PortName port) =>
        node.FindPort(port) is { } declared && Ports.CarriesText(declared.Type)
            ? declared.Value ?? _startTexts.Text(port) ?? _inputs.Text(port)
            : null;
}
