using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.State;
using Kuroe.Execution.Shared.Usage;

namespace Kuroe.Execution.Shared.Restore;

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