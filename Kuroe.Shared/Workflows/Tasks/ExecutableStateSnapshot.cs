using Kuroe.Shared.Workflows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>一个执行节点在某时刻的执行状态：实例展开集与完成进度。</summary>
public sealed record ExecutableStateSnapshot(
    int Index,
    NodeState State,
    IReadOnlyList<int> Items,
    int CompletedItems);
