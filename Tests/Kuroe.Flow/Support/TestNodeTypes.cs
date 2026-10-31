using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的节点类型：与执行节点同形的替身。
/// 派发执行的类型由扩展提供，引擎不自带。</summary>
public static class TestNodeTypes
{
    /// <summary>测试用的执行节点类型名。</summary>
    public static NodeKind ExecutableKind { get; } = new("Executable");

    /// <summary>测试用的执行节点类型实例。</summary>
    public static INodeType Executable { get; } = new TestExecutableNodeType();

    /// <summary>测试用的执行节点类型实例，不带静态输入，用于只验证执行与接线规则的节点。</summary>
    public static INodeType PlainExecutable { get; } = new TestExecutableNodeType(withContractInput: false);

    /// <summary>测试用的容器类型：组织子节点，不派发执行。</summary>
    public static INodeType Container { get; } = new TestNodeType(
        new NodeKind("Container"),
        new NodeTypeShape
        {
            Organizes = true,
            InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
            OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        });
}
