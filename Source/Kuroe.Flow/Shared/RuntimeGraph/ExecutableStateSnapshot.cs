using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一个节点在某时刻的执行状态。</summary>
public sealed record ExecutableStateSnapshot(
    NodePath Node,
    NodeState State);
