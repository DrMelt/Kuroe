using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

namespace Kuroe.Flow.Persistence.Records.Shared;

/// <summary>一次执行的持久状态。恢复数据作为执行历史保留。</summary>
public sealed record ExecutionRecord(
    ExecutionId Id,
    NodePath Node,
    int ExecutionCount,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    ExecutionUsage Usage,
    IReadOnlyList<InputValueRecord> Inputs,
    IReadOnlyDictionary<PortName, string> Texts,
    ExecutionRestorePayload? RestoreData = null,
    ExecutionId? ContinuedFrom = null,
    ExecutionContractRecord? Contract = null);

/// <summary>一次执行的本轮契约状态。</summary>
public sealed record ExecutionContractRecord(
    IReadOnlyDictionary<PortName, PortValue>? Ports = null,
    string? CannotCompleteReason = null);

/// <summary>一次执行进入某个输入端口的取值与出处。</summary>
public sealed record InputValueRecord(
    PortName Port,
    ContextSourceRecord Origin,
    string Text);

/// <summary>取值出处的持久形态。</summary>
public sealed record ContextSourceRecord(
    ContextSourceKind Kind,
    ExecutionId? Execution,
    NodePath? Node,
    PortName? Port);

/// <summary>取值出处的种类。</summary>
public enum ContextSourceKind
{
    /// <summary>执行产出的出处。</summary>
    Execution,

    /// <summary>输入节点回答的出处。</summary>
    Input,

    /// <summary>流程起点输入的出处。</summary>
    Start,

    /// <summary>容器命名输出端口的出处。</summary>
    ContainerPort,

    /// <summary>节点输入端口默认值的出处。</summary>
    Default,

    /// <summary>节点输入端口常量绑定的出处。</summary>
    Constant,

    /// <summary>不派发执行的节点自身产出的出处。</summary>
    Node,
}
