using System.Text.Json.Serialization;
using Kuroe.Flow.Graph.Records;

namespace Kuroe.Flow.Persistence.Graph;

/// <summary>图流程定义的存档形状。</summary>
internal sealed record GraphFlowDefinitionData(string? Name, string? Nodes, string? Flow);

/// <summary>图流程状态的存档形状。</summary>
internal sealed record GraphFlowStateData(
    TaskStartData? Start,
    List<NodeData>? Nodes,
    List<PortPublicationData>? Ports,
    SchedulerSeedData? Pending);

/// <summary>任务开始状态的形状。</summary>
internal sealed record TaskStartData(List<StartInputData>? Inputs, Dictionary<string, string>? StartTexts);

/// <summary>开始输入的形状。</summary>
internal sealed record StartInputData(string? Port, string? Text);

/// <summary>节点记录的形状。节点类型专属字段按种类使用。</summary>
internal sealed record NodeData(
    string? Kind,
    NodePathData? Path,
    NodeCommonData? Common,
    NodeBlockData? Block,
    [property: JsonPropertyName("ReworkTargets")] List<NodePathData>? RerunTargets,
    bool? Waiting,
    NodeInputValuesData? Inputs,
    bool? Awaiting);

/// <summary>节点阻塞原因的形状。</summary>
internal sealed record NodeBlockData(string? Summary, int? Execution, DateTimeOffset? At);

/// <summary>节点公共状态的形状。</summary>
internal sealed record NodeCommonData(
    long Revision,
    int Executions,
    int? LastInputGroup,
    List<ConsumedRevisionData>? Consumed,
    long? ActivePublicationRevision,
    bool ForceRerun);

/// <summary>一次发布在一个输出端口上的形状。</summary>
internal sealed record PortPublicationData(
    NodePathData? Node,
    string? Port,
    long Revision,
    DateTimeOffset At,
    bool Trigger,
    List<PublicationValueData>? Values);

/// <summary>发布内容的形状。</summary>
internal sealed record PublicationValueData(ContextSourceData? Origin, string? Text);

/// <summary>已消费版本的形状。</summary>
internal sealed record ConsumedRevisionData(NodePathData? Source, string? Port, long Revision);

/// <summary>节点路径的形状。</summary>
internal sealed record NodePathData(List<string>? Levels);

/// <summary>审批节点输入内容的形状。</summary>
internal sealed record NodeInputValuesData(List<NodeInputValueData>? Ordered);

/// <summary>审批节点一条输入取值的形状。</summary>
internal sealed record NodeInputValueData(string? Target, string? Type, InputValueData? Value);

/// <summary>输入取值的形状。</summary>
internal sealed record InputValueData(string? Port, ContextSourceData? Origin, string? Text);

/// <summary>输入取值出处的形状。</summary>
internal sealed record ContextSourceData(
    ContextSourceKind Kind,
    int? Execution,
    NodePathData? Node,
    string? Port);

/// <summary>调度种子的形状。</summary>
internal sealed record SchedulerSeedData(List<NodePathData>? Targets);