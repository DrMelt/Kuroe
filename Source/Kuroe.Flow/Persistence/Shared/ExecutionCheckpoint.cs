using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>一次执行的持久状态。</summary>
public sealed record ExecutionCheckpoint(
    ExecutionId Id,
    NodePath Node,
    int ExecutionCount,
    ExecutionState State,
    string Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    IReadOnlyList<string> Failures,
    IReadOnlyList<JournalEntry> Journal,
    int DroppedEntries,
    IReadOnlyList<InputValueCheckpoint> Inputs,
    IReadOnlyDictionary<PortName, string> Texts);

/// <summary>一次执行进入某个输入端口的取值与出处。</summary>
public sealed record InputValueCheckpoint(
    PortName Port,
    ContextSourceCheckpoint Origin,
    string Text);

/// <summary>取值出处的持久形态。</summary>
public sealed record ContextSourceCheckpoint(
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
