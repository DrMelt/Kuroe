using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Persistence.Serialization.Shared;

/// <summary>存档文档：版本与任务形状。</summary>
public sealed record TaskRecordDocument(int Version, TaskRecordData? Task);

/// <summary>任务存档的通用形状。</summary>
public sealed record TaskRecordData(
    TaskIdentityData? Identity,
    DateTimeOffset? SavedAt,
    FlowSnapshotData? Flow,
    TaskStartData? Start,
    TaskLifecycle Lifecycle,
    bool Completed,
    TaskJournalData? Journal,
    List<ExecutionData>? Executions,
    List<NodeData>? Nodes,
    SchedulerSeedData? Pending);

/// <summary>任务身份的形状。</summary>
public sealed record TaskIdentityData(
    string? Id,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastActivityAt);

/// <summary>流程快照的形状。</summary>
public sealed record FlowSnapshotData(string? Name, string? Nodes, string? Flow);

/// <summary>任务开始状态的形状。</summary>
public sealed record TaskStartData(List<StartInputData>? Inputs, Dictionary<string, string>? StartTexts);

/// <summary>开始输入的形状。</summary>
public sealed record StartInputData(string? Port, string? Text);

/// <summary>任务过程记录的形状。</summary>
public sealed record TaskJournalData(List<JournalEntryData>? Entries, int DroppedEntries);

/// <summary>一条过程记录的形状。</summary>
public sealed record JournalEntryData(DateTimeOffset At, JournalLevel Level, string? Label, string? Text);

/// <summary>一次执行的形状。</summary>
public sealed record ExecutionData(
    int Id,
    NodePathData? Node,
    int ExecutionCount,
    ExecutionState State,
    string? Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    List<string>? Failures,
    List<JournalEntryData>? Journal,
    int DroppedEntries,
    ExecutionUsageData? Usage,
    List<InputValueData>? Inputs,
    Dictionary<string, string>? Texts);

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

/// <summary>输入取值的形状。</summary>
public sealed record InputValueData(string? Port, ContextSourceData? Origin, string? Text);

/// <summary>输入取值出处的形状。</summary>
public sealed record ContextSourceData(
    ContextSourceKind Kind,
    int? Execution,
    NodePathData? Node,
    string? Port);

/// <summary>节点记录的形状。节点类型专属字段按种类使用。</summary>
public sealed record NodeData(
    string? Kind,
    NodePathData? Path,
    NodeCommonData? Common,
    NodeBlockData? Block,
    List<NodePathData>? ReworkTargets,
    bool? Waiting,
    NodeInputValuesData? Inputs,
    bool? Awaiting);

/// <summary>节点阻塞原因的形状。</summary>
public sealed record NodeBlockData(string? Summary, int? Execution, DateTimeOffset? At);

/// <summary>节点公共状态的形状。</summary>
public sealed record NodeCommonData(
    long Revision,
    int Executions,
    int? LastInputGroup,
    List<ConsumedRevisionData>? Consumed,
    PublicationData? Publication,
    bool ForceRerun);

/// <summary>最近一次发布的形状。</summary>
public sealed record PublicationData(
    Dictionary<string, bool>? Triggers,
    Dictionary<string, List<PublicationValueData>>? Content);

/// <summary>发布内容的形状。</summary>
public sealed record PublicationValueData(ContextSourceData? Origin, string? Text);

/// <summary>已消费版本的形状。</summary>
public sealed record ConsumedRevisionData(NodePathData? Source, string? Port, long Revision);

/// <summary>节点路径的形状。</summary>
public sealed record NodePathData(List<string>? Levels);

/// <summary>审批节点输入内容的形状。</summary>
public sealed record NodeInputValuesData(List<NodeInputValueData>? Ordered);

/// <summary>审批节点一条输入取值的形状。</summary>
public sealed record NodeInputValueData(string? Target, string? Type, InputValueData? Value);

/// <summary>调度种子的形状。</summary>
public sealed record SchedulerSeedData(List<NodePathData>? Targets);
