using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>一个节点在任务内的持久状态。公共部分与节点类型专属状态分开。</summary>
public abstract record NodeRecord(NodePath Path, NodeCommonRecord Common);

/// <summary>各节点共有的持久状态：输入侧进展与最近一次发布。</summary>
public sealed record NodeCommonRecord(
    long Revision,
    int Executions,
    int? LastInputGroup,
    IReadOnlyList<ConsumedRevisionRecord> Consumed,
    PublicationRecord? Publication,
    bool ForceRerun);

/// <summary>最近一次发布的持久形态：触发位与各输出端口的内容。</summary>
public sealed record PublicationRecord(
    IReadOnlyDictionary<PortName, bool> Triggers,
    IReadOnlyDictionary<PortName, IReadOnlyList<PublicationValueRecord>> Content);

/// <summary>发布内容的一段取值：出处与文本。</summary>
public sealed record PublicationValueRecord(
    ContextSourceRecord Origin,
    string Text);

/// <summary>一条已消费版本：来源节点、来源端口与消费到的版本。</summary>
public sealed record ConsumedRevisionRecord(
    NodePath Source,
    PortName? Port,
    long Revision);

/// <summary>派发执行节点的持久状态。</summary>
public sealed record ExecutableNodeRecord(
    NodePath Path,
    NodeCommonRecord Common,
    IReadOnlyList<NodePath> ReworkTargets,
    NodeBlock? Block)
    : NodeRecord(Path, Common);

/// <summary>自驱型节点的持久状态。</summary>
public sealed record SelfDrivenNodeRecord(
    NodePath Path,
    NodeCommonRecord Common,
    NodeBlock? Block,
    bool FailureTaken)
    : NodeRecord(Path, Common);

/// <summary>输入节点的持久状态。</summary>
public sealed record InputNodeRecord(
    NodePath Path,
    NodeCommonRecord Common,
    bool Waiting)
    : NodeRecord(Path, Common);

/// <summary>审批节点的持久状态。</summary>
public sealed record ApprovalNodeRecord(
    NodePath Path,
    NodeCommonRecord Common,
    NodeInputValuesRecord Inputs,
    bool Awaiting)
    : NodeRecord(Path, Common);

/// <summary>容器的持久状态。内容就绪由成员状态推导，不单独保存。</summary>
public sealed record ContainerNodeRecord(
    NodePath Path,
    NodeCommonRecord Common)
    : NodeRecord(Path, Common);

/// <summary>审批节点停驻时保存的输入内容。</summary>
public sealed record NodeInputValuesRecord(
    IReadOnlyList<NodeInputValueRecord> Ordered);

/// <summary>审批节点的一条输入取值。</summary>
public sealed record NodeInputValueRecord(
    PortName Target,
    string Type,
    InputValueRecord Value);
