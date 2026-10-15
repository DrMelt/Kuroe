using Kuroe.Flow.NodeType;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Tests.Support;

/// <summary>测试用的节点类型注册表：引擎内置的容器与测试自带的执行节点类型。
/// 派发 run 的类型由扩展提供，引擎不自带，测试注册一个与执行节点同形的替身。</summary>
internal static class TestNodeTypes
{
    /// <summary>测试用的执行节点类型名。</summary>
    internal static NodeKind Executable { get; } = new("Executable");

    /// <summary>建出含引擎内置类型与替身的注册表。</summary>
    internal static NodeTypeRegistry Builtin()
    {
        NodeTypeRegistry types = new();
        BuiltinTypeRegistry.RegisterInto(types);
        types.Register(Executable, () => new TestExecutableNodeType());

        return types;
    }
}
