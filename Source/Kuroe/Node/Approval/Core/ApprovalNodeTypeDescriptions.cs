using Kuroe.FlowDescription.Shared.Compilation.NodeTypes;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Node.Approval;

/// <summary>审批节点描述的注册入口。</summary>
public static class ApprovalNodeTypeDescriptions
{
    /// <summary>把审批节点的描述注册进注册表。</summary>
    public static void RegisterInto(
        INodeTypeDescriptionRegistry files,
        IExecutionRunnerFactory runnerFactory) =>
        files.Register(new ApprovalNodeTypeDescription(runnerFactory));
}