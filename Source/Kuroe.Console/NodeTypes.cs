using Kuroe.Flow.Graph.Shared.RuntimeGraph.Execution;
using Kuroe.Host.FlowIntegration.GraphFlow.NodeExecution;
using Kuroe.FlowDescription.Compilation.Shared.NodeTypes;
using Kuroe.Host.Ability.Node.Approval;
using Kuroe.Host.Ability.Node.Input;

namespace Kuroe.Console;

/// <summary>Console 追加的等待型节点类型。</summary>
internal static class NodeTypes
{
    /// <summary>把输入与审批节点描述与图能力注册进流程装配。</summary>
    public static void Register(
        INodeTypeDescriptionRegistry types,
        GraphNodeCapabilities capabilities,
        FlowNodeExecutionRunners runners)
    {
        InputNodeTypeDescriptions.RegisterInto(types);
        ApprovalNodeTypeDescriptions.RegisterInto(types);
        FlowNodeExecution.RegisterInteractive(capabilities, runners);
    }
}
