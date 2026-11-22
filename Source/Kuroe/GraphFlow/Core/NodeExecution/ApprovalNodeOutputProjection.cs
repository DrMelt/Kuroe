using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.GraphFlow.NodeExecution;

/// <summary>审批节点的输出投影：把停驻输入按同名输出端口投影为节点发布，保留出处与多段内容，不携带触发。</summary>
public sealed class ApprovalNodeOutputProjection : INodeOutputProjection
{
    /// <inheritdoc/>
    public Publication Project(ExecutionId execution, GraphExecutionInput input, ExecutionOutput output)
    {
        Dictionary<PortName, List<ContextValue>> byPort = [];
        foreach (InputValue value in input.Inputs)
        {
            if (!byPort.TryGetValue(value.Port, out List<ContextValue>? values))
            {
                values = [];
                byPort[value.Port] = values;
            }

            values.Add(value.Value);
        }

        Dictionary<PortName, IReadOnlyList<ContextValue>> content = input.OutputPorts
            .Where(port => byPort.ContainsKey(port.Name))
            .ToDictionary(port => port.Name, port => (IReadOnlyList<ContextValue>)byPort[port.Name]);

        return new Publication(0, new Dictionary<PortName, bool>(), content);
    }
}
