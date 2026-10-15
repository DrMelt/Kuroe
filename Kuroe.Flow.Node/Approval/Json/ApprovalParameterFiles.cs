using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Flow.Node.Approval.Json;

/// <summary>审批节点参数段形状的注册入口。</summary>
public static class ApprovalParameterFiles
{
    /// <summary>把审批节点的参数段形状注册进注册表。</summary>
    public static void RegisterInto(NodeParameterFileRegistry files) => files.Register(new ApprovalParameterFile());
}