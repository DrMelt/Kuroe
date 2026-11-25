using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Journal;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Usage;

namespace Kuroe.Kernel.Execution.Shared.State;

/// <summary>一次执行的只读形状：请求、状态、过程记录、用量、恢复数据与契约。</summary>
public sealed record ExecutionSnapshot(
    ExecutionId Id,
    ExecutionRequest Request,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    IReadOnlyList<JournalEntry> Journal,
    ExecutionUsage Usage,
    ExecutionRestoreState? RestoreData,
    ExecutionContract Contract,
    ExecutionOutput? Output,
    ExecutionId? ContinuedFrom);