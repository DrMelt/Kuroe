using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Tasks.Records;
using LoopExecution = Kuroe.Flow.Execution.Execution;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从存档恢复任务并加入任务域。未完成的任务启动推进循环；完成与终止的任务只恢复状态，不再推进。</summary>
public sealed class TaskRestoreRuntime(
    TaskRegistry registry,
    IFlowFrameworkSource frameworks,
    IFlowRuntime runner) : ITaskRestoreRuntime
{
    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> Restore(TaskRecord record)
    {
        ErrorOr<FlowProvider> resolved = frameworks.Find(record.Flow);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        FlowProvider provider = resolved.Value;
        ErrorOr<FlowRestore> restored = provider.Submission.Restore(record);
        if (restored.IsError)
        {
            return restored.ErrorsOrEmptyList;
        }

        WorkTask task = WorkTask.Restore(
            registry,
            record,
            provider,
            restored.Value.State,
            restored.Value.StartTexts,
            restored.Value.Executions);
        registry.Adopt(task);
        foreach (LoopExecution execution in task.Executions)
        {
            registry.RestoreExecution(task, execution);
        }

        if (!record.Completed && record.Lifecycle != TaskLifecycle.Terminated)
        {
            runner.Restore(task, restored.Value.Seed);
        }

        task.NotifyChanged();

        return task.Snapshot();
    }
}