using ErrorOr;
using Kuroe.Flow.Compilation.Shared.Validation;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;

namespace Kuroe.Flow.NodeType.Container;

/// <summary>容器类型：组织成员并限定作用域，自身不执行，成员齐备即汇合。引擎内置，运行期对象由引擎建立。</summary>
internal sealed class ContainerNodeType : INodeType, INodeTypeValidation
{
    /// <summary>类型名。</summary>
    public NodeKind Kind => ContainerNodeTypes.Name;

    /// <summary>容器类型的静态形状。</summary>
    public NodeTypeShape Shape => ContainerNodeTypes.Shape;

    /// <summary>容器的类型规则：统一执行上限、输入端口默认值与输出端口绑定。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        if (node.MaxExecutions is { } limit && limit < 1)
        {
            fail(scope.Node(node.Label.Value, "MaxExecutions 必须是正整数。"));
        }

        foreach (InputPort port in node.Inputs.Where(port => port.Default is not null))
        {
            fail(scope.Node(node.Label.Value, $"容器输入端口 {port.Name} 不支持默认值。"));
        }
    }
}
