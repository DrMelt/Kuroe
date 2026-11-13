using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Control.Graph.Engine;
using ExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Control.Graph.Contract;

/// <summary>执行的本轮契约状态读取：缺口与已上报的无法完成原因只在此算一次，回执、追问与查看工具共用同一口径。</summary>
public sealed class ExecutionStatusReader(IFlowExecutionGateway executions) : IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在时返回错误。</summary>
    public ErrorOr<ExecutionStatus> Read(ExecutionId id)
    {
        if (executions.FindOwned(id) is not { IsError: false } found)
        {
            return Error.Validation("Tool.Argument", $"没有执行 {id}。");
        }

        ITaskFlowContext task = found.Value.Owner;

        return task.UnderLock(() => Of(task, found.Value.Execution));
    }

    /// <summary>在已持有任务锁时读出本轮契约状态。要求持有任务 Gate。</summary>
    public static ExecutionStatus Of(ITaskFlowContext task, IExecution execution)
    {
        GraphFlowControl control = task.Control as GraphFlowControl
            ?? throw new InvalidOperationException($"任务 {task.Id} 没有图流程控制。");

        return control.Status(execution);
    }
}