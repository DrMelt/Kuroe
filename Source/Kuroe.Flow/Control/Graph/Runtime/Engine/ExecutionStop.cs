using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using ExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>没有活跃推进循环时按执行号停止一次执行。未启动的执行不构成一次尝试，不能停止。</summary>
internal static class ExecutionStop
{
    public static ErrorOr<Success> Stop(WorkTask task, ExecutionId id)
    {
        GraphExecution? execution = task.UnderLock(() =>
            task.Executions.FirstOrDefault(candidate => candidate.Id == id));
        if (execution is null)
        {
            return [ExecutionErrors.ExecutionNotFound(id.Value)];
        }

        if (!execution.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(execution.Id, "停止")];
        }

        if (execution.Snapshot().StartedAt is null)
        {
            return [ExecutionErrors.ExecutionNotStarted(execution.Id)];
        }

        execution.Interrupt("已停止执行。");

        return Result.Success;
    }
}