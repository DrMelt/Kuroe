using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的节点类型：与执行节点同形的替身。
/// 派发执行的类型由扩展提供，引擎不自带。</summary>
public static class TestNodeTypes
{
    /// <summary>测试用的执行节点类型名。</summary>
    public static NodeKind ExecutableKind { get; } = new("Executable");

    /// <summary>测试用的执行节点类型实例。</summary>
    public static INodeType Executable { get; } = new TestExecutableNodeType();
}
