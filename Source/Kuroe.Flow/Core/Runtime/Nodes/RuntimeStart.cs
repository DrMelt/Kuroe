using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>开始节点在任务内的运行时对象：任务启动即发布一次，产出是任务启动时确定的开始端口值，
/// 固定值、宿主输入与提交输入共用同一份解析。版本恒为 1，下游只消费一次。不派发执行、不等待、不取消。</summary>
internal sealed class RuntimeStart : RuntimeNode
{
    private readonly StartNode _start;
    private readonly IPortTexts _startPorts;

    public RuntimeStart(StartNode node, IPortTexts startPorts) : base(node)
    {
        _start = node;
        _startPorts = startPorts;
        Progress.Publish(CreatePublication());
    }

    public override bool Canceled => false;

    protected override bool Consumable => true;

    public override void Cancel()
    {
    }

    /// <summary>开始节点的产出由任务开始输入重建，不单独持久化。</summary>
    public override NodeRecord? CaptureRecord() => null;

    /// <summary>开始端口值由任务开始快照保存，不写入端口历史。</summary>
    public override IReadOnlyList<PortPublicationRecord> CapturePorts() => [];

    /// <summary>开始节点没有需要从存档恢复的状态。</summary>
    public override void RestoreRecord(NodeRecord record, INodeRestoreContext context)
    {
    }

    /// <summary>开始节点不从端口历史恢复。</summary>
    public override void RestorePorts(IReadOnlyList<PortPublicationRecord> records)
    {
        // 开始端口不写入端口历史，恢复时没有记录可装载。
    }

    /// <summary>开始端口的发布内容：承载文本的端口按启动值各给一条产出，出处指向流程起点。</summary>
    private Publication CreatePublication()
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = [];
        foreach (Port port in _start.Outputs)
        {
            if (!port.IsConstant && !PortRules.CarriesText(port.Type))
            {
                continue;
            }

            if (_startPorts.Text(port.Name) is { Length: > 0 } text)
            {
                content[port.Name] = [new ContextValue(new StartSource(port.Name), text)];
            }
        }

        return new Publication(1, new Dictionary<PortName, bool>(), content);
    }
}