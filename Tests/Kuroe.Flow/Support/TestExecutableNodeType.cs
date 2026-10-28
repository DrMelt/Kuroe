using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;

namespace Kuroe.Flow.TestSupport;

/// <summary>测试用的执行节点类型：派发执行，端口取值域与执行节点一致，不接受任何类型专属声明。</summary>
public sealed class TestExecutableNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind => TestNodeTypes.ExecutableKind;

    /// <summary>测试执行节点声明的静态输入端口。</summary>
    public static PortName StaticPort { get; } = new("静态值");

    /// <summary>派发执行，输入输出与执行节点同形，静态输入带测试默认值。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        DispatchesExecutions = true,
        StaticInputs =
        [
            new InputPort(StaticPort, PortTypes.Text, "测试静态输入。", "执行者", isStatic: true),
        ],
        InputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
        OutputTypes = [PortTypes.Text, PortTypes.Context, PortTypes.Signal],
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