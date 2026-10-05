using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Tasks;

/// <summary>一个节点在任务内的运行时对象：节点定义与节点自身状态的合体。
/// 执行节点由推进方法改动状态，容器随成员推进经 TaskRuntime 刷新。读写都要求持有任务 Gate。</summary>
internal abstract class RuntimeNode(GraphNode node)
{
    /// <summary>提交时锁定的节点定义，不可变。</summary>
    public GraphNode Node { get; } = node;

    /// <summary>节点在图里的序号，框架路由与快照定位以它为准。</summary>
    public int Index => Node.Index;

    /// <summary>节点名。</summary>
    public NodeName Name => Node.Name;

    /// <summary>节点产出后是否停在等待批准。</summary>
    public NodeGate Gate => Node.Gate;

    /// <summary>产出已放行、可被下游消费：执行节点按自身发布与停驻，容器递归成员与子容器。</summary>
    public abstract bool Released { get; }

    /// <summary>整节点的产出版本：执行节点给发表号，容器给齐备代数。版本是消费账的版本依据。</summary>
    public abstract long Revision { get; }

    /// <summary>产出停在等待批准。</summary>
    public abstract bool Awaiting { get; }

    /// <summary>随任务取消。</summary>
    public abstract bool Canceled { get; }

    /// <summary>已放行产出的位置集合：执行节点给整节点或已发布实例，容器递归给全部成员。</summary>
    public abstract IReadOnlyList<(int Node, int? Item)> ReleasedOutputs();

    /// <summary>随任务进入取消态。</summary>
    public abstract void Cancel();
}
