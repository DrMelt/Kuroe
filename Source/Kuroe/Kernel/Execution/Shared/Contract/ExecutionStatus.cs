using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Execution.Shared.Contract;

/// <summary>一次执行在本轮的契约状态：步骤、执行状态、尚缺的输出端口与无法完成原因。</summary>
public sealed record ExecutionStatus(
    ExecutionStepId Step,
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
            request.Step,
            state,
            outstanding,
            contract.CannotCompleteReason);
    }
}