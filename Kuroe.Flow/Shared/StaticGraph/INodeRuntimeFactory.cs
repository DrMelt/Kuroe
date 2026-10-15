using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>不派发 run 的节点类型自建运行期对象：停驻与产出在此表达。
/// 组织成员的容器与派发 run 的执行节点，其运行期对象由引擎按形状建立。</summary>
public interface INodeRuntimeFactory
{
    /// <summary>建立该节点的运行期对象。</summary>
    NodeRuntime Create(GraphNode node, INodeContext context);
}