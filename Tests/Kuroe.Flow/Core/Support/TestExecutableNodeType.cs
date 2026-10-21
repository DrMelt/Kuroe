using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.Tests.Support;

/// <summary>测试用的执行节点类型：派发执行，端口取值域与执行节点一致，不接受任何类型专属声明。</summary>
internal sealed class TestExecutableNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind => TestNodeTypes.ExecutableKind;

    /// <summary>派发执行，输入输出与执行节点同形。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
        OutputTypes = [PortType.Text, PortType.Context, PortType.Signal],
    };

    /// <summary>替身的类型规则：执行上限必须是正整数，覆盖类型规则经节点作用域报出的路径。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        if (node.Spec?.MaxExecutions is { } limit && limit < 1)
        {
            fail(scope.Node(node.Label.Value, "MaxExecutions 必须是正整数。"));
        }
    }
}