using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.Node.Approval;

/// <summary>审批节点类型的对外入口：类型名与注册。审批节点不启动 run，批准后按输入端口转发产出。</summary>
public static class ApprovalNodeTypes
{
    /// <summary>类型名，与流程文件里 Kind 的取值对应。</summary>
    public static NodeTypeName Name { get; } = new("Approval");

    /// <summary>把审批类型注册进注册表。</summary>
    public static void RegisterInto(NodeTypeRegistry registry) => registry.Register(new ApprovalNodeType());
}
