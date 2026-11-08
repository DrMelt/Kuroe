
namespace Kuroe.Flow.Shared.RuntimeGraph.Execution;

/// <summary>一次执行的只读形状。</summary>
public sealed record ExecutionSnapshot(
    ExecutionId Id,
    ExecutionInput Input,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    IReadOnlyList<JournalEntry> Journal,
    ExecutionUsage Usage,
    bool HasRestoreState = false,
    ExecutionId? ContinuedFrom = null)
{
    /// <summary>已耗时，未结束时按当前时刻算。</summary>
    public TimeSpan Elapsed => ElapsedAt(DateTimeOffset.UtcNow);

    /// <summary>到给定时刻为止的已耗时，未结束时按该时刻算。</summary>
    public TimeSpan ElapsedAt(DateTimeOffset now) => (FinishedAt ?? now) - (StartedAt ?? CreatedAt);
}
