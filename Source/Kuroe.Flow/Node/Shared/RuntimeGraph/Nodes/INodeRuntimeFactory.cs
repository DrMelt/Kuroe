using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>节点类型自行建立运行期对象的能力。派发执行与组织成员的类型由引擎按形状建立，不实现它。</summary>
public interface INodeRuntimeFactory
{
    /// <summary>建立该节点的运行期对象：停驻与产出的表达。</summary>
    NodeRuntime Create(GraphNode node, INodeContext context);
}
