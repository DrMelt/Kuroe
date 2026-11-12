using ErrorOr;
using Kuroe.Flow.Execution;
using LoopExecution = Kuroe.Flow.Execution.Execution;

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
        ErrorOr<LoopExecution> found = registry.FindExecution(id);

        return found.IsError ? found.ErrorsOrEmptyList : found.Value.Snapshot();
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExecutionSnapshot> LiveExecutions() => registry.LiveExecutions();

    /// <inheritdoc/>
    public IReadOnlyList<TaskSnapshot> Snapshots() => registry.Snapshots();
}
