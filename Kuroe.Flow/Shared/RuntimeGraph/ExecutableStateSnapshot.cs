using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>一个节点在某时刻的执行状态：实例展开集、完成进度与停在阻塞的条目。</summary>
public sealed record ExecutableStateSnapshot(
    NodePath Node,
    NodeState State,
    IReadOnlyList<int> Items,
    int CompletedItems,
    IReadOnlyList<int> BlockedItems);
