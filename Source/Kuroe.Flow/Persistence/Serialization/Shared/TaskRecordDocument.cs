using System.Text.Json;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档文档：版本与任务形状。</summary>
public sealed record TaskRecordDocument(int Version, TaskRecordData? Task);

/// <summary>任务存档的通用形状。</summary>
public sealed record TaskRecordData(
    TaskIdentityData? Identity,
    DateTimeOffset? SavedAt,
    TaskLifecycle Lifecycle,
    bool Completed,
    TaskJournalData? Journal,
    FlowData? Flow,
    List<ExecutionData>? Executions);

/// <summary>任务身份的形状。</summary>
public sealed record TaskIdentityData(
    string? Id,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

/// <summary>任务过程记录的形状。</summary>
public sealed record TaskJournalData(List<JournalEntryData>? Entries, int DroppedEntries);

/// <summary>一条过程记录的形状。</summary>
public sealed record JournalEntryData(
    DateTimeOffset At,
    JournalLevel Level,
    string? Label,
    string? Text);

/// <summary>流程记录的形状。定义与状态由流程实现自己的编解码器解释。</summary>
public sealed record FlowData(
    string? Name,
    string? Kind,
    string? Implementation,
    JsonElement? Definition,
    JsonElement? State);

/// <summary>一次执行的形状。</summary>
public sealed record ExecutionData(
    int Id,
    string? Target,
    int Attempt,
    ExecutionState State,
    string? Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    List<string>? Failures,
    ExecutionUsageData? Usage,
    List<ExecutionInputData>? Inputs,
    Dictionary<string, string>? Texts,
    RestorePayloadData? RestoreData = null,
    int? ContinuedFrom = null,
    ExecutionContractData? Contract = null);

/// <summary>一次执行进入某个输入端口的取值与来源标识。</summary>
public sealed record ExecutionInputData(string? Port, string? Source, string? Text);

/// <summary>一次执行保留的恢复负载。</summary>
public sealed record RestorePayloadData(string? Format, string? Payload);

/// <summary>一次执行的本轮契约状态。</summary>
public sealed record ExecutionContractData(
    List<PortValueData>? Outputs,
    string? CannotCompleteReason);

/// <summary>一个交回端口的值。</summary>
public sealed record PortValueData(string? Port, string? Text, bool? Trigger);

/// <summary>一次执行的模型请求用量的形状。</summary>
public sealed record ExecutionUsageData(
    int RequestCount,
    int RequestsWithoutUsage,
    TokenUsageData? Tokens,
    long? ContextTokens);

/// <summary>token 用量的形状。</summary>
public sealed record TokenUsageData(
    long? Input,
    long? Output,
    long? Total,
    long? CachedInput,
    long? Reasoning);
