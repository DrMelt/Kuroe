using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>流程开始端口在任务内的来源状态：任务启动即已放行，版本恒为 1，按动态承载内容的端口给出任务启动时确定的内容。</summary>
internal sealed class RuntimeStartSource : ISourceState
{
    private readonly Publication _publication;

    public RuntimeStartSource(IReadOnlyList<Port> ports, IPortTexts startPorts)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = [];
        foreach (Port port in ports)
        {
            if (port.IsConstant || !PortRules.CarriesContent(port.Type))
            {
                continue;
            }

            if (startPorts.Text(port.Name) is { Length: > 0 } text)
            {
                content[port.Name] = [new ContextValue(new StartSource(port.Name), text)];
            }
        }

        _publication = new Publication(1, new Dictionary<PortName, bool>(), content);
    }

    /// <inheritdoc/>
    public bool Released => true;

    /// <inheritdoc/>
    public long Revision => 1;

    /// <inheritdoc/>
    public bool Triggers(PortName port) => true;

    /// <summary>开始端口的内容产出。</summary>
    public IReadOnlyList<ContextValue> OutputValues(PortName port) => _publication.Output(port);
}