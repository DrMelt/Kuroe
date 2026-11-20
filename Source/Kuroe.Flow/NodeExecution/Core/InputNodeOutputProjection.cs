using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.NodeExecution;

/// <summary>输入节点的输出投影：把用户回答投影为承载文本的输出端口，出处指向本节点的输入。</summary>
public sealed class InputNodeOutputProjection : INodeOutputProjection
{
    /// <summary>共享实例。</summary>
    public static InputNodeOutputProjection Instance { get; } = new();

    /// <inheritdoc/>
    public Publication Project(ExecutionId execution, GraphExecutionInput input, ExecutionOutput output)
    {
        Dictionary<PortName, IReadOnlyList<ContextValue>> content = output.Content.Length > 0
            ? input.OutputPorts
                .Where(port => PortRules.CarriesText(port.Type))
                .ToDictionary(
                    port => port.Name,
                    port => (IReadOnlyList<ContextValue>)
                        [new ContextValue(new InputSource(input.Node, port.Name), output.Content)])
            : [];

        return new Publication(0, new Dictionary<PortName, bool>(), content);
    }
}
