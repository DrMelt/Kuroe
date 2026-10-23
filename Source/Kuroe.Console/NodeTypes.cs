using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Node.Approval;
using Kuroe.Node.Input;

namespace Kuroe.Console;

/// <summary>Console 追加的等待型节点类型。</summary>
internal static class NodeTypes
{
    /// <summary>把输入与审批节点描述注册进流程编译器。</summary>
    public static void Register(INodeTypeDescriptionRegistry types, IInputChannel channel)
    {
        InputNodeTypeDescriptions.RegisterInto(types, channel);
        ApprovalNodeTypeDescriptions.RegisterInto(types, channel);
    }
}