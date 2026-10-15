using ErrorOr;
using Kuroe.Flows.RuntimeNodes;
using Kuroe.Flows.Shared;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.StaticGraph;
using ContainerNode = Kuroe.Flows.Shared.StaticGraph.ContainerNode;

namespace Kuroe.Flows.StaticGraph.Types;

/// <summary>容器类型：组织成员并限定作用域，自身不执行，成员齐备即汇合。引擎自带的机制型类型。</summary>
internal sealed class ContainerNodeType : INodeType
{
    /// <summary>类型名。</summary>
    public NodeTypeName Type => NodeTypes.Container;

    /// <summary>组织子节点，不派发 run，输入端口声明由引用处绑定。</summary>
    public NodeTypeShape Shape { get; } = new()
    {
        Organizes = true,
        InputTypes = [PortType.Text, PortType.Context, PortType.Signal],
    };

    /// <summary>容器的类型规则：统一执行上限与输出端口绑定。</summary>
    public void Validate(NodeSpec node, NodeScope scope, Action<Error> fail)
    {
        if (node.MaxRuns is { } limit && limit < 1)
        {
            fail(scope.Node(node.Name.Value, "MaxRuns 必须是正整数。"));
        }
    }

    /// <summary>容器的运行期对象。</summary>
    public NodeRuntime Create(GraphNode node, INodeContext context) => new RuntimeContainer((ContainerNode)node, context);
}
