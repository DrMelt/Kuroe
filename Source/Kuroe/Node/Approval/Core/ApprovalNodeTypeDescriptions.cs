using Kuroe.Flow.Compilation.Shared.NodeTypes;
using Kuroe.Flow.InputChannel.Shared;

namespace Kuroe.Node.Approval;

/// <summary>审批节点描述的注册入口。</summary>
public static class ApprovalNodeTypeDescriptions
{
    /// <summary>把审批节点的描述注册进注册表。外部输入协议是读描述建类型实例所需的运行期依赖，由装配处给出。</summary>
    public static void RegisterInto(INodeTypeDescriptionRegistry files, IInputChannel channel) =>
        files.Register(new ApprovalNodeTypeDescription(channel));
}