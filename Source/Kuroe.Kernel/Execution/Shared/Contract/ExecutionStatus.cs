using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Execution.Shared.Contract;

/// <summary>一次执行在本轮的契约状态：目标、执行状态、尚缺的输出端口与无法完成原因。</summary>
public sealed record ExecutionStatus(
    ExecutionTargetId Target,
    ExecutionState State,
    IReadOnlyList<ExecutionPortName> OutstandingOutputs,
    string? CannotCompleteReason)
{
    /// <summary>按执行请求、当前契约与执行状态算出本轮状态。</summary>
    public static ExecutionStatus Of(
        ExecutionRequest request,
        ExecutionContract contract,
        ExecutionState state)
    {
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs =
            contract.Outputs ?? new Dictionary<ExecutionPortName, ExecutionOutputValue>();
        IReadOnlyList<ExecutionPortName> outstanding =
        [
            .. request.OutputPorts
                .Where(port => port.Required && !outputs.ContainsKey(port.Name))
                .Select(port => port.Name),
        ];

        return new ExecutionStatus(
            request.Target,
            state,
            outstanding,
            contract.CannotCompleteReason);
    }
}