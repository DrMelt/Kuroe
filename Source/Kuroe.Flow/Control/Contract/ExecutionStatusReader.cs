using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Execution.Shared;
using ExecutionStatus = Kuroe.Execution.Shared.ExecutionStatus;

namespace Kuroe.Flow.Control.Contract;

/// <summary>执行的本轮契约状态读取：缺口与已上报的无法完成原因只在此算一次，回执、追问与查看工具共用同一口径。</summary>
public sealed class ExecutionStatusReader(IExecutionOwnerIndex executions) : IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在时返回错误。</summary>
    public ErrorOr<ExecutionStatus> Read(ExecutionId id)
    {
        if (executions.FindOwned(id) is not { IsError: false } found)
        {
            return Error.Validation("Tool.Argument", $"没有执行 {id}。");
        }

        (IExecution execution, ITaskFlowContext task) = found.Value;
        lock (task.Gate)
        {
            return Of(execution);
        }
    }

    /// <summary>按执行请求与执行契约算出本轮状态，要求持有任务 Gate。</summary>
    internal static ExecutionStatus Of(IExecution execution)
    {
        ExecutionContract contract = execution.Contract;
        IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs =
            contract.Outputs ?? new Dictionary<ExecutionPortName, ExecutionOutputValue>();
        IReadOnlyList<ExecutionPortName> outstanding =
        [
            .. execution.Request.OutputPorts
                .Where(port => port.Required && !outputs.ContainsKey(port.Name))
                .Select(port => port.Name),
        ];

        return new ExecutionStatus(
            execution.Request.Target,
            execution.Request.Attempt,
            execution.Request.MaxAttempts,
            execution.State,
            outstanding,
            contract.CannotCompleteReason);
    }
}