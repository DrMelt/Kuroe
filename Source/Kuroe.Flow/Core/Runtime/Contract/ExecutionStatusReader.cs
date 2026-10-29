using ErrorOr;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>执行的本轮契约状态读取：缺口与已上报的无法完成原因只在此算一次，回执、追问与查看工具共用同一口径。</summary>
public sealed class ExecutionStatusReader(ExecutionIndex executions) : IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在时返回错误。</summary>
    public ErrorOr<ExecutionStatus> Read(ExecutionId id)
    {
        if (executions.FindOwned(id) is not { IsError: false } found)
        {
            return Error.Validation("Tool.Argument", $"没有执行 {id}。");
        }

        WorkTask task = found.Value.Owner;

        return task.UnderLock(() => Of(task, found.Value.Execution));
    }

    /// <summary>在已持有任务锁时读出本轮契约状态。要求持有任务 Gate。</summary>
    public static ExecutionStatus Of(WorkTask task, IExecution execution)
    {
        RuntimeExecutable node = task.Runtime.Node(execution);

        return new ExecutionStatus(
            execution.Input.Node,
            execution.Input.ExecutionCount,
            task.Runtime.Plan(execution.Input.Node)?.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
            node.Snapshot().State,
            node.Shortfall(execution.Id),
            node.CannotCompleteReason(execution.Id));
    }
}