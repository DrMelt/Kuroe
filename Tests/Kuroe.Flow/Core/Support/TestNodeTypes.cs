using Kuroe.Flow.Shared.StaticGraph.Node;

namespace Kuroe.Flow.Tests.Support;

/// <summary>测试用的节点类型：引擎内置的容器与测试自带的执行节点类型。
/// 派发执行的类型由扩展提供，引擎不自带，测试用一个与执行节点同形的替身。</summary>
internal static class TestNodeTypes
{
    /// <summary>测试用的执行节点类型名。</summary>
    internal static NodeKind ExecutableKind { get; } = new("Executable");

    /// <summary>测试用的执行节点类型实例。</summary>
    internal static INodeType Executable { get; } = new TestExecutableNodeType();
}
