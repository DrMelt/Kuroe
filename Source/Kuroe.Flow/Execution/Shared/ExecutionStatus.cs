namespace Kuroe.Flow.Execution.Shared;

/// <summary>一次执行在本轮的契约状态：目标、轮次与上限、执行状态、尚缺的输出端口与无法完成原因。</summary>
public sealed record ExecutionStatus(
    ExecutionTargetId Target,
    int Attempt,
    int MaxAttempts,
    ExecutionState State,
    IReadOnlyList<ExecutionPortName> OutstandingOutputs,
    string? CannotCompleteReason);