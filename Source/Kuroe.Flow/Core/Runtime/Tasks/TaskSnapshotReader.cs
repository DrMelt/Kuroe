using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务索引的只读快照面：把任务索引的领域查询转成快照，交给信息查询工具。</summary>
public sealed class TaskSnapshotReader(TaskRegistry registry) : ITaskReader
{
    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> Find(TaskId id)
    {
        ErrorOr<WorkTask> found = registry.Find(id);

        return found.IsError ? found.ErrorsOrEmptyList : found.Value.Snapshot();
    }

    /// <inheritdoc/>
    public ErrorOr<TaskSnapshot> FindByReference(string reference)
    {
        ErrorOr<WorkTask> found = registry.FindByReference(reference);

        return found.IsError ? found.ErrorsOrEmptyList : found.Value.Snapshot();
    }

    /// <inheritdoc/>
    public ErrorOr<ExecutionSnapshot> FindExecution(ExecutionId id)
    {
        IExecution? found = registry.FindExecution(id);

        return found is null ? [ExecutionErrors.ExecutionNotFound(id.Value)] : found.Snapshot();
    }

    /// <inheritdoc/>
    public ErrorOr<(TaskSnapshot Task, ExecutionSnapshot Execution)> FindOwnedExecution(ExecutionId id)
    {
        ErrorOr<(WorkTask Task, IExecution Execution)> found = registry.FindOwnedExecution(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        return (found.Value.Task.Snapshot(), found.Value.Execution.Snapshot());
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExecutionSnapshot> LiveExecutions() => registry.LiveExecutions();

    /// <inheritdoc/>
    public IReadOnlyList<TaskSnapshot> Snapshots() => registry.Snapshots();
}
