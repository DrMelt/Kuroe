using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>开始节点在任务内的运行时对象：任务启动即已放行，产出是任务启动时确定的开始端口值，
/// 固定值、宿主输入与提交输入共用同一份解析。版本恒为 1，下游只消费一次。不派发执行、不等待、不取消。</summary>
internal sealed class RuntimeStart(StartNode node, IPortTexts startPorts) : RuntimeNode(node)
{
    /// <summary>任务启动时确定的开始端口文本，固定值、宿主输入与提交输入按端口给出。</summary>
    private readonly IPortTexts _startPorts = startPorts;

    public override bool Released => true;

    public override long Revision => 1;

    public override bool Canceled => false;

    /// <summary>按端口取任务启动时确定的文本，未声明端口或既非 Text 也非常量端口时无产出。</summary>
    public override string? PortText(INodeContext context, PortName port) => Text(port);

    /// <summary>按端口取初始输入消息，出处指向流程起点。</summary>
    public override IReadOnlyList<ContextValue> OutputValues(INodeContext context, PortName port) =>
        Text(port) is { Length: > 0 } text
            ? [new ContextValue(new StartSource(port), text)]
            : [];

    public override void Cancel()
    {
    }

    /// <summary>开始节点的产出由任务开始输入重建，不单独持久化。</summary>
    public override NodeCheckpoint? CaptureCheckpoint() => null;

    /// <summary>开始节点没有需要从检查点恢复的状态。</summary>
    public override void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context)
    {
    }

    private string? Text(PortName port) =>
        node.FindPort(port) is { } declared && (declared.IsConstant || PortRules.CarriesText(declared.Type))
            ? _startPorts.Text(port)
            : null;
}
