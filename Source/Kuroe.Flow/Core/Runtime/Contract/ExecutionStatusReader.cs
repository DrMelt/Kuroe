using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using NeutralExecutionId = Kuroe.Flow.Execution.ExecutionId;
using NeutralExecutionState = Kuroe.Flow.Execution.ExecutionState;
using NeutralExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>执行的本轮契约状态读取：缺口与已上报的无法完成原因只在此算一次，回执、追问与查看工具共用同一口径。</summary>
public sealed class ExecutionStatusReader(ExecutionIndex executions) : Kuroe.Flow.Execution.IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在时返回错误。</summary>
    public ErrorOr<NeutralExecutionStatus> Read(NeutralExecutionId id)
    {
        if (executions.FindOwned(new Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId(id.Value)) is not { IsError: false } found)
        {
            return Error.Validation("Tool.Argument", $"没有执行 {id}。");
        }

        WorkTask task = found.Value.Owner;

        return task.UnderLock(() => Of(task, found.Value.Execution));
    }

    /// <summary>在已持有任务锁时读出本轮契约状态。要求持有任务 Gate。</summary>
    public static NeutralExecutionStatus Of(WorkTask task, GraphExecution execution)
    {
        RuntimeExecutable node = task.Runtime.Node(execution);
        Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionContract contract = execution.Contract;

        return new NeutralExecutionStatus(
            execution.Request.Target,
            execution.Request.Attempt,
            task.Runtime.Plan(execution.Input.Node)?.MaxExecutions ?? ExecutableNode.DefaultMaxExecutions,
            (NeutralExecutionState)execution.State,
            [.. node.OutstandingPorts(execution).Select(name => new ExecutionPortName(name.Value))],
            contract.CannotCompleteReason);
    }
}