using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一个节点在某时刻的执行状态。</summary>
public sealed record ExecutableStateSnapshot(
    NodePath Node,
    NodeState State);
