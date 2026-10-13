using Kuroe.Flows.Shared.Execution;

namespace Kuroe.Flows.Shared.RuntimeGraph;

/// <summary>一个执行节点在某时刻的执行状态：实例展开集、完成进度、停在阻塞的条目、等待批准的产出与输入节点的回答。</summary>
public sealed record ExecutableStateSnapshot(
    int Index,
    NodeState State,
    IReadOnlyList<int> Items,
    int CompletedItems,
    IReadOnlyList<int> BlockedItems,
    IReadOnlyList<RunId> AwaitingRuns,
    string? InputAnswer);
