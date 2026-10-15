using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.Shared.RuntimeGraph;

/// <summary>引擎给节点实现的机制入口：读图与来源、取输入侧方案、记消费账、通知下游。读写要求持有任务 Gate。</summary>
public interface INodeContext : IExecutionSources
{
    /// <summary>任务的流程编译视图。</summary>
    INodeGraph Graph { get; }

    /// <summary>某序号的输入侧方案，开始与不参与就绪判定的节点为空。</summary>
    NodeInputPlan? Plan(int nodeIndex);

    /// <summary>某序号的运行期节点，未实例化时按图定义建出，任务已取消时落在取消态。</summary>
    NodeRuntime Runtime(int nodeIndex);

    /// <summary>某序号已实例化的运行期节点，未实例化时为空。只读路径用它，不建实例。</summary>
    NodeRuntime? Materialized(int nodeIndex);
}
