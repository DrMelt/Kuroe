using Kuroe.Flow.Node.Approval;
using Kuroe.Flow.Node.Input;
using Kuroe.Flow.Node.Llm;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.NodeType;

namespace Kuroe.Tests.Support;

/// <summary>测试用的节点类型注册表：引擎内置类型加扩展提供的执行与等待型类型。</summary>
internal static class TestNodeTypes
{
    /// <summary>建出一份含全部可用类型的注册表。</summary>
    internal static NodeTypeRegistry Registry()
    {
        NodeTypeRegistry types = new();
        BuiltinTypeRegistry.RegisterInto(types);
        LlmNodeTypes.RegisterInto(types);
        InputNodeTypes.RegisterInto(types);
        ApprovalNodeTypes.RegisterInto(types);

        return types;
    }
}
