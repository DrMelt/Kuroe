using Kuroe.Execution.Shared;

namespace Kuroe.Execution.Records.Shared;

/// <summary>一次执行的持久状态。执行记录只使用中性执行端口与不透明来源标识。</summary>
public sealed record ExecutionRecord(
    ExecutionId Id,
    ExecutionTargetId Target,
    int Attempt,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    ExecutionUsageRecord Usage,
    IReadOnlyList<ExecutionInputRecord> Inputs,
    IReadOnlyDictionary<ExecutionPortName, string> Texts,
    ExecutionRestoreRecord? RestoreData = null,
    ExecutionId? ContinuedFrom = null,
    ExecutionContractRecord? Contract = null);

/// <summary>一次执行进入某个输入端口的取值与不透明来源标识。</summary>
public sealed record ExecutionInputRecord(
    ExecutionPortName Port,
    string Source,
    string Text);

/// <summary>一次执行保留的恢复负载，格式由执行目标类型解释。</summary>
public sealed record ExecutionRestoreRecord(
    string Format,
    string Payload);

/// <summary>一次执行的模型请求用量。</summary>
public sealed record ExecutionUsageRecord(
    int RequestCount,
    int RequestsWithoutUsage,
    TokenUsageRecord Tokens,
    long? ContextTokens);

/// <summary>一次执行累计的模型 token 用量。</summary>
public sealed record TokenUsageRecord(
    long? Input,
    long? Output,
    long? Total,
    long? CachedInput,
    long? Reasoning);

/// <summary>一次执行的本轮契约状态。</summary>
public sealed record ExecutionContractRecord(
    IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue>? Outputs = null,
    string? CannotCompleteReason = null);