using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Persistence;

/// <summary>启动加载：逐文件读取检查点，获取租约、编译流程快照并恢复任务。</summary>
public sealed class TaskRestoreService(
    ITaskCheckpointStore store,
    IFlowSnapshotCompiler compiler,
    ITaskLeaseStore leases,
    ITaskRestoreRuntime runtime) : ITaskRestoreService, IDisposable
{
    private readonly List<ITaskLease> _held = [];
    private bool _restored;

    /// <inheritdoc/>
    public TaskLoadReport Restore()
    {
        if (_restored)
        {
            return new TaskLoadReport([], [], []);
        }

        _restored = true;
        TaskStoreReadResult read = store.ReadAll();
        List<TaskSnapshot> loaded = [];
        List<TaskId> busy = [];
        List<TaskLoadFailure> failures =
        [
            .. read.Failures.Select(failure => new TaskLoadFailure(failure.Source, failure.Errors)),
        ];

        foreach (TaskCheckpointDocument document in read.Documents)
        {
            RestoreOne(document.Task, loaded, busy, failures);
        }

        return new TaskLoadReport(loaded, busy, failures);
    }

    private void RestoreOne(
        TaskCheckpoint checkpoint,
        List<TaskSnapshot> loaded,
        List<TaskId> busy,
        List<TaskLoadFailure> failures)
    {
        string source = $"{checkpoint.Identity.Id.Value}.json";
        ErrorOr<ITaskLease> lease = leases.TryAcquire(checkpoint.Identity.Id);
        if (lease.IsError)
        {
            busy.Add(checkpoint.Identity.Id);
            return;
        }

        ErrorOr<CompiledFlow> flow = compiler.Compile(checkpoint.Flow);
        if (flow.IsError)
        {
            failures.Add(new TaskLoadFailure(source, flow.ErrorsOrEmptyList));
            lease.Value.Dispose();
            return;
        }

        ErrorOr<TaskSnapshot> task = runtime.Restore(checkpoint, flow.Value, checkpoint.Pending.Targets);
        if (task.IsError)
        {
            failures.Add(new TaskLoadFailure(source, task.ErrorsOrEmptyList));
            lease.Value.Dispose();
            return;
        }

        _held.Add(lease.Value);
        loaded.Add(task.Value);
    }

    /// <summary>释放加载期间持有的任务租约。</summary>
    public void Dispose()
    {
        foreach (ITaskLease lease in _held)
        {
            lease.Dispose();
        }

        _held.Clear();
    }
}
