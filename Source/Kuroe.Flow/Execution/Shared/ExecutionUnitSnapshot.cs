using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Execution;

/// <summary>一次执行的只读形状：请求、状态、过程记录、用量、恢复数据与契约。</summary>
public sealed record ExecutionUnitSnapshot(
    ExecutionId Id,
    ExecutionRequest Request,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    IReadOnlyList<JournalEntry> Journal,
    ExecutionUsageSnapshot Usage,
    ExecutionRestoreState? RestoreData,
    ExecutionContract Contract,
    ExecutionOutput? Output,
    ExecutionId? ContinuedFrom);

/// <summary>一次执行的模型请求用量快照。</summary>
public sealed record ExecutionUsageSnapshot(
    int RequestCount,
    int RequestsWithoutUsage,
    TokenUsageSnapshot Tokens,
    long? ContextTokens)
{
    /// <summary>没有发起模型请求的形态。</summary>
    public static ExecutionUsageSnapshot None { get; } = new(0, 0, TokenUsageSnapshot.None, null);
}

/// <summary>模型 token 用量快照。</summary>
public sealed record TokenUsageSnapshot(
    long? Input,
    long? Output,
    long? Total,
    long? CachedInput,
    long? Reasoning)
{
    /// <summary>没有取得任何用量的形态。</summary>
    public static TokenUsageSnapshot None { get; } = new(null, null, null, null, null);
}

/// <summary>一次执行保留的恢复负载快照。格式由执行目标类型解释。</summary>
public sealed record ExecutionRestoreState(
    string Format,
    string Payload);