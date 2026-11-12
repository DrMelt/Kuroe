using ErrorOr;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从存档恢复任务并加入任务域。未完成的任务启动推进循环；完成与终止的任务只恢复状态，不再推进。</summary>
public sealed class TaskRestoreRuntime(
    TaskRegistry registry,
    ITaskRestorer restorer,
    IFlowRunner runner,
    Kuroe.Flow.Execution.ExecutionRestorerRegistry restorers) : ITaskRestoreRuntime
{
    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> Restore(
        TaskRecord record,
        CompiledFlow flow,
        IReadOnlyList<NodePath> seed)
    {
        registry.Restorers = restorers;
        ErrorOr<WorkTask> task = restorer.Restore(record, flow);
        if (task.IsError)
        {
            return task.ErrorsOrEmptyList;
        }

        registry.Adopt(task.Value);
        if (task.Value.State is not (TaskState.Done or TaskState.Terminated))
        {
            IReadOnlyList<NodePath> restart =
            [
                .. task.Value.Executions
                    .Where(execution => execution.State == ExecutionState.Interrupted
                        && execution.Snapshot().StartedAt is null)
                    .Select(execution => execution.Input.Node)
                    .Distinct(),
            ];
            runner.Restore(task.Value, [.. seed, .. restart]);
        }

        task.Value.NotifyChanged();

        return task.Value.Snapshot();
    }
}
