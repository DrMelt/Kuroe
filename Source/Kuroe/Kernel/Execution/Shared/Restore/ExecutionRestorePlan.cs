using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.State;
using Kuroe.Kernel.Execution.Shared.Usage;

namespace Kuroe.Kernel.Execution.Shared.Restore;

/// <summary>从存档重建一次执行所需的中性数据。</summary>
public sealed record ExecutionRestorePlan(
    ExecutionId Id,
    ExecutionRequest Request,
    DateTimeOffset CreatedAt,
    ExecutionState State,
    string Progress,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    ExecutionUsage Usage,
    ExecutionRestoreState? RestoreData,
    ExecutionContract Contract,
    ExecutionId? ContinuedFrom);