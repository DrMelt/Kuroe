using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph;
using ContainerNode = Kuroe.Flow.Shared.StaticGraph.ContainerNode;

namespace Kuroe.Flow.NodeType.Container;

/// <summary>容器类型：组织成员并限定作用域，自身不执行，成员齐备即汇合。引擎内置，运行期对象由引擎建立。</summary>
internal sealed class ContainerNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeKind Kind => ContainerNodeTypes.Name;

    /// <summary>容器类型的静态形状。</summary>
    public NodeTypeShape Shape => ContainerNodeTypes.Shape;

    /// <summary>容器的类型规则：统一执行上限与输出端口绑定。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        if (node.MaxRuns is { } limit && limit < 1)
        {
            fail(scope.Node(node.Name.Value, "MaxRuns 必须是正整数。"));
        }
    }
}
