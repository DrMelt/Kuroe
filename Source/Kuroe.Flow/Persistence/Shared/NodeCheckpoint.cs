using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>一个节点在任务内的持久状态。公共部分与节点类型专属状态分开。</summary>
public abstract record NodeCheckpoint(NodePath Path, NodeCommonCheckpoint Common);

/// <summary>各节点共有的持久状态：输入侧进展与最近一次发布。</summary>
public sealed record NodeCommonCheckpoint(
    long Revision,
    int Executions,
    int? LastInputGroup,
    IReadOnlyList<ConsumedRevisionCheckpoint> Consumed,
    PublicationCheckpoint? Publication,
    bool ForceRerun);

/// <summary>最近一次发布的持久形态：触发位与各输出端口的内容。</summary>
public sealed record PublicationCheckpoint(
    IReadOnlyDictionary<PortName, bool> Triggers,
    IReadOnlyDictionary<PortName, IReadOnlyList<PublicationValueCheckpoint>> Content);

/// <summary>发布内容的一段取值：出处与文本。</summary>
public sealed record PublicationValueCheckpoint(
    ContextSourceCheckpoint Origin,
    string Text);

/// <summary>一条已消费版本：来源节点、来源端口与消费到的版本。</summary>
public sealed record ConsumedRevisionCheckpoint(
    NodePath Source,
    PortName? Port,
    long Revision);

/// <summary>派发执行节点的持久状态。</summary>
public sealed record ExecutableNodeCheckpoint(
    NodePath Path,
    NodeCommonCheckpoint Common,
    IReadOnlyList<NodePath> ReworkTargets,
    bool Blocked)
    : NodeCheckpoint(Path, Common);

/// <summary>自驱型节点的持久状态。</summary>
public sealed record SelfDrivenNodeCheckpoint(
    NodePath Path,
    NodeCommonCheckpoint Common,
    string? Failure,
    bool FailureTaken)
    : NodeCheckpoint(Path, Common);

/// <summary>输入节点的持久状态。</summary>
public sealed record InputNodeCheckpoint(
    NodePath Path,
    NodeCommonCheckpoint Common,
    bool Waiting)
    : NodeCheckpoint(Path, Common);

/// <summary>审批节点的持久状态。</summary>
public sealed record ApprovalNodeCheckpoint(
    NodePath Path,
    NodeCommonCheckpoint Common,
    NodeInputValuesCheckpoint Inputs,
    bool Awaiting)
    : NodeCheckpoint(Path, Common);

/// <summary>容器的持久状态。内容就绪由成员状态推导，不单独保存。</summary>
public sealed record ContainerNodeCheckpoint(
    NodePath Path,
    NodeCommonCheckpoint Common)
    : NodeCheckpoint(Path, Common);

/// <summary>审批节点停驻时保存的输入取值。</summary>
public sealed record NodeInputValuesCheckpoint(
    IReadOnlyList<NodeInputValueCheckpoint> Ordered,
    IReadOnlyList<PortName> Connected,
    IReadOnlyDictionary<PortName, bool> Triggers,
    IReadOnlyDictionary<PortName, string> StaticTexts);

/// <summary>审批节点的一条输入取值。</summary>
public sealed record NodeInputValueCheckpoint(
    PortName Target,
    string Type,
    InputValueCheckpoint Value);
