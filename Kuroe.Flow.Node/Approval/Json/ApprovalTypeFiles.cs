using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Flow.Node.Approval.Json;

/// <summary>审批节点文件形状的注册入口。</summary>
public static class ApprovalTypeFiles
{
    /// <summary>把审批节点的文件形状注册进注册表。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files) => files.Register(new ApprovalTypeFile());
}