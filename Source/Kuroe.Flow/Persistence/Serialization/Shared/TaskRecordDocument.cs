using System.Text.Json.Serialization;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Graph.Records;
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
public sealed record JournalEntryData(DateTimeOffset At, JournalLevel Level, string? Label, string? Text);

/// <summary>流程记录的形状。</summary>
public sealed record FlowData(
    string? Kind,
    GraphFlowDefinitionData? GraphDefinition,
    GraphFlowStateData? GraphState,
    CodeFlowDefinitionData? CodeDefinition = null,
    CodeFlowStateData? CodeState = null);

/// <summary>代码流程定义的形状。</summary>
public sealed record CodeFlowDefinitionData(
    string? Implementation,
    Dictionary<string, string>? Values);

/// <summary>代码流程状态的形状。</summary>
public sealed record CodeFlowStateData(
    TaskStartData? Start,
    Dictionary<string, string>? Values);

/// <summary>图流程定义的形状。</summary>
public sealed record GraphFlowDefinitionData(string? Name, string? Nodes, string? Flow);

/// <summary>图流程状态的形状。</summary>
public sealed record GraphFlowStateData(
    TaskStartData? Start,
    List<NodeData>? Nodes,
    List<PortPublicationData>? Ports,
    SchedulerSeedData? Pending);

/// <summary>任务开始状态的形状。</summary>
public sealed record TaskStartData(List<StartInputData>? Inputs, Dictionary<string, string>? StartTexts);

/// <summary>开始输入的形状。</summary>
public sealed record StartInputData(string? Port, string? Text);

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
    [property: JsonPropertyName("ReworkTargets")] List<NodePathData>? RerunTargets,
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
    long? ActivePublicationRevision,
    bool ForceRerun);

/// <summary>一次发布在一个输出端口上的形状。</summary>
public sealed record PortPublicationData(
    NodePathData? Node,
    string? Port,
    long Revision,
    DateTimeOffset At,
    bool Trigger,
    List<PublicationValueData>? Values);

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
