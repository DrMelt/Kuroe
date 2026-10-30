using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Nodes;

/// <summary>带检查点的运行时节点基类。检查点状态由节点实现捕获与恢复。</summary>
public abstract class RuntimeNode(GraphNode node) : NodeRuntime(node), INodeCheckpointSource
{
    /// <inheritdoc/>
    public abstract NodeCheckpoint? CaptureCheckpoint();

    /// <inheritdoc/>
    public abstract void RestoreCheckpoint(NodeCheckpoint checkpoint, INodeRestoreContext context);
}
