using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Readiness;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>引擎给节点实现的机制入口：来源状态、输入侧方案与运行期对象。读写要求持有任务 Gate。</summary>
public interface INodeContext : IReadinessSources
{
    /// <summary>某节点的输入侧方案，开始为空。</summary>
    NodeInputPlan? Plan(NodePath node);

    /// <summary>某节点的运行期节点，未实例化时按图定义建出，任务已取消时落在取消态。</summary>
    NodeRuntime Runtime(NodePath node);

    /// <summary>某节点已实例化的运行期节点，未实例化时为空。只读路径用它，不建实例。</summary>
    NodeRuntime? Materialized(NodePath node);
}
