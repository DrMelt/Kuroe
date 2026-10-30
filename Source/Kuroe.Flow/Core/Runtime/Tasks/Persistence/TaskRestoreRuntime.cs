using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从检查点恢复任务，加入任务域并恢复推进循环。</summary>
public sealed class TaskRestoreRuntime(
    TaskRegistry registry,
    ITaskRestorer restorer,
    IFlowRunner runner) : ITaskRestoreRuntime
{
    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> Restore(
        TaskCheckpoint checkpoint,
        CompiledFlow flow,
        IReadOnlyList<NodePath> seed)
    {
        ErrorOr<WorkTask> task = restorer.Restore(checkpoint, flow);
        if (task.IsError)
        {
            return task.ErrorsOrEmptyList;
        }

        registry.Adopt(task.Value);
        runner.Restore(task.Value, seed);

        return task.Value.Snapshot();
    }
}
