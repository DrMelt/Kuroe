using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Execution.Shared.Contract;

/// <summary>一次执行在本轮的契约状态：目标、执行状态、尚缺的输出端口与无法完成原因。</summary>
public sealed record ExecutionStatus(
    ExecutionTargetId Target,
    ExecutionState State,
    IReadOnlyList<ExecutionPortName> OutstandingOutputs,
    string? CannotCompleteReason);