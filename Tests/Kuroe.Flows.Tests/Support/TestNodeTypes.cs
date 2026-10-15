using Kuroe.Flows.Shared.StaticGraph;
using Kuroe.Flows.StaticGraph.Types;

namespace Kuroe.Flows.Tests.Support;

/// <summary>测试用的节点类型注册表：引擎内置的执行与容器。等待型类型由宿主提供，其规则在各自的库测试里验证。</summary>
internal static class TestNodeTypes
{
    /// <summary>建出含引擎内置类型的注册表。</summary>
    internal static NodeTypeRegistry Builtin()
    {
        NodeTypeRegistry types = new();
        BuiltinNodeTypes.RegisterInto(types);

        return types;
    }
}
