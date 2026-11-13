namespace Kuroe.Flow.Execution;

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

/// <summary>一次执行保留的恢复负载快照。格式由执行目标类型解释。</summary>
public sealed record ExecutionRestoreState(
    string Format,
    string Payload);