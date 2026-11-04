using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Persistence.Restoring.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Persistence.Restoring;

/// <summary>启动加载：逐文件读取存档，获取租约、编译流程快照并恢复任务。</summary>
public sealed class TaskRestoreService(
    ITaskRecordStore store,
    IFlowSnapshotCompiler compiler,
    ITaskLeaseStore leases,
    ITaskRestoreRuntime runtime) : ITaskRestoreService
{
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
            .. read.Failures.Select(failure => new TaskLoadFailure(null, failure.Source, failure.Errors)),
        ];

        foreach (TaskRecord record in read.Records)
        {
            RestoreOne(record, loaded, busy, failures);
        }

        return new TaskLoadReport(loaded, busy, failures);
    }

    private void RestoreOne(
        TaskRecord record,
        List<TaskSnapshot> loaded,
        List<TaskId> busy,
        List<TaskLoadFailure> failures)
    {
        string source = record.Identity.Id.Value;
        ErrorOr<Success> lease = leases.TryAcquire(record.Identity.Id);
        if (lease.IsError)
        {
            if (lease.FirstError.Type == ErrorType.Conflict)
            {
                busy.Add(record.Identity.Id);
            }
            else
            {
                failures.Add(new TaskLoadFailure(record.Identity.Id, source, lease.ErrorsOrEmptyList));
            }

            return;
        }

        ErrorOr<CompiledFlow> flow = compiler.Compile(record.Flow);
        if (flow.IsError)
        {
            failures.Add(new TaskLoadFailure(record.Identity.Id, source, flow.ErrorsOrEmptyList));
            leases.Release(record.Identity.Id);
            return;
        }

        ErrorOr<TaskSnapshot> task = runtime.Restore(record, flow.Value, record.Pending.Targets);
        if (task.IsError)
        {
            failures.Add(new TaskLoadFailure(record.Identity.Id, source, task.ErrorsOrEmptyList));
            leases.Release(record.Identity.Id);
            return;
        }

        loaded.Add(task.Value);
    }
}
