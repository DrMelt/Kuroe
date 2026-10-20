using ErrorOr;
using Kuroe.Flow.RuntimeGraph;
using Kuroe.Flow.RuntimeNodes;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.StaticGraph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>执行的本轮契约状态读取：缺口与已上报的无法完成原因只在此算一次，回执、追问与查看工具共用同一口径。</summary>
public sealed class ExecutionStatusReader(TaskRegistry registry) : IExecutionStatusReader
{
    /// <summary>按执行号读出本轮契约状态，执行不存在或没有归属任务时返回错误。</summary>
    public ErrorOr<ExecutionStatus> Read(ExecutionId id)
    {
        if (registry.FindExecution(id) is not { IsError: false } found)
        {
            return Error.Validation("Tool.Argument", $"没有执行 {id}。");
        }

        if (registry.Find(found.Value.Input.Task) is not { IsError: false } owner)
        {
            return Error.Unexpected("Tool.Internal", "该执行没有归属任务。");
        }

        WorkTask task = owner.Value;

        return task.UnderLock(() => Of(task, found.Value));
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
            TaskRuntime.Shortfall(node, execution.Id).Ports,
            node.CannotCompleteReason(execution.Id));
    }
}