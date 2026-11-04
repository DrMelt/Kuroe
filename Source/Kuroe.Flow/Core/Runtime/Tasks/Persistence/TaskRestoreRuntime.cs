using ErrorOr;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从存档恢复任务，加入任务域并恢复推进循环。</summary>
public sealed class TaskRestoreRuntime(
    TaskRegistry registry,
    ITaskRestorer restorer,
    IFlowRunner runner) : ITaskRestoreRuntime
{
    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> Restore(
        TaskRecord record,
        CompiledFlow flow,
        IReadOnlyList<NodePath> seed)
    {
        ErrorOr<WorkTask> task = restorer.Restore(record, flow);
        if (task.IsError)
        {
            return task.ErrorsOrEmptyList;
        }

        registry.Adopt(task.Value);
        runner.Restore(task.Value, seed);
        task.Value.NotifyChanged();

        return task.Value.Snapshot();
    }
}
