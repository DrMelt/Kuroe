using Kuroe.Flow.InputChannel;
using Kuroe.Flow.Json.Parameters;

namespace Kuroe.Node.Approval.Json;

/// <summary>审批节点文件形状的注册入口。</summary>
public static class ApprovalTypeFiles
{
    /// <summary>把审批节点的文件形状注册进注册表。外部输入协议是读文件建类型实例所需的运行期依赖，由装配处给出。</summary>
    public static void RegisterInto(NodeTypeFileRegistry files, IInputChannel channel) =>
        files.Register(new ApprovalTypeFile(channel));
}