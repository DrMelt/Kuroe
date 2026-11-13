using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务动作：提交、暂停、恢复、重新执行、终止与改名。流程语义经 <see cref="IFlowIdentity"/> 落地。</summary>
public sealed class TaskActions(
    TaskRegistry registry,
    IExecutionRuntime executions,
    IFlowRuntime driver,
    IFlowProviderSource providers,
    ITaskIdSource ids,
    ITaskRecordCoordinator? records = null,
    ITaskLeaseStore? leases = null)
{
    private readonly TaskRegistry _registry = registry;
    private readonly IExecutionRuntime _executions = executions;
    private readonly IFlowRuntime _driver = driver;
    private readonly IFlowProviderSource _providers = providers;
    private readonly ITaskIdSource _ids = ids;
    private readonly ITaskRecordCoordinator? _records = records;
    private readonly ITaskLeaseStore? _leases = leases;

    /// <summary>按流程名提交：取负责的流程实现，再按实现提交。observer 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> Submit(
        FlowName flowName,
        StartInputs inputs,
        string? title,
        ProcessObserver? observer = null)
    {
        ErrorOr<FlowProvider> resolved = _providers.Resolve(flowName);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        return Submit(resolved.Value, flowName, inputs, title, observer);
    }

    /// <summary>按已选定的流程实现提交：分配任务号、由实现准备状态、占用任务键，再建任务并启动推进。</summary>
    public ErrorOr<TaskSnapshot> Submit(
        FlowProvider provider,
        FlowName flowName,
        StartInputs inputs,
        string? title,
        ProcessObserver? observer = null)
    {
        TaskId id = _ids.Next();
        ErrorOr<FlowSubmission> prepared = provider.Submission.Prepare(id, flowName, inputs, title);
        if (prepared.IsError)
        {
            return prepared.ErrorsOrEmptyList;
        }

        FlowSubmission submission = prepared.Value;

        if (_leases is { } leaseStore)
        {
            ErrorOr<Success> lease = leaseStore.TryAcquire(id);
            if (lease.IsError)
            {
                return lease.ErrorsOrEmptyList;
            }
        }

        WorkTask task = new(
            _registry,
            _executions,
            id,
            submission.State.Inputs,
            submission.StartTexts,
            submission.Title,
            provider,
            submission.State,
            submission.Definition,
            observer);
        _registry.Adopt(task);

        lock (task.Gate)
        {
            _driver.Start(task);
        }

        // 提交是任务身份首次跨会话可见的点，等待这一份存档落盘；其余动作只标记待写，由后台合并写盘
        PersistImmediately(task.Id);

        return task.Snapshot();
    }

    /// <summary>标记任务状态已变，由存档协调器在后台写盘，交互路径不等写盘。</summary>
    private void Persist(TaskId id) => _records?.MarkDirty(id);

    /// <summary>写入该任务并等待落盘，用于任务身份变更与终止等需要立即可见的动作。</summary>
    private void PersistImmediately(TaskId id) => _records?.FlushAsync(id).GetAwaiter().GetResult();
    /// <summary>暂停任务：停止新的推进与派发，在跑的执行就地中断。</summary>
    public ErrorOr<Success> Pause(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            ErrorOr<Success> paused = task.Pause();
            if (paused.IsError)
            {
                return paused;
            }

            _driver.Pause(task);
        }

        Persist(task.Id);

        return Result.Success;
    }

    /// <summary>恢复任务：暂停时恢复控制状态，存在恢复计划时派发恢复执行并唤醒推进。</summary>
    public ErrorOr<Success> Resume(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        lock (task.Gate)
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                return [TaskErrors.Terminated(id)];
            }

            if (task.Lifecycle == TaskLifecycle.Paused)
            {
                ErrorOr<Success> resumed = task.Resume();
                if (resumed.IsError)
                {
                    return resumed;
                }
            }
            else if (task.Lifecycle != TaskLifecycle.Active
                || task.Completed
                || task.Control?.HasRestores != true)
            {
                return [TaskErrors.NotPaused(id)];
            }

            _driver.ResumeTask(task);
        }

        Persist(task.Id);

        return Result.Success;
    }

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        found.Value.Title = title;
        Persist(found.Value.Id);

        return Result.Success;
    }

    /// <summary>对阻塞的步骤重新执行，放弃恢复，保留恢复数据。暂停任务先重新执行再恢复推进。</summary>
    public ErrorOr<Success> Rerun(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        int rerun;
        lock (task.Gate)
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                return [TaskErrors.Terminated(id)];
            }

            rerun = _driver.Rerun(task);
            if (rerun == 0)
            {
                return [TaskErrors.NotBlocked(id)];
            }

            if (task.Lifecycle == TaskLifecycle.Paused)
            {
                ErrorOr<Success> resumed = task.Resume();
                if (resumed.IsError)
                {
                    return resumed;
                }

                _driver.ResumeTask(task, continueRestores: false);
            }
        }

        Persist(id);

        return Result.Success;
    }

    /// <summary>停止一个已启动的执行。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id)
    {
        ErrorOr<(WorkTask Task, IExecution Execution)> found = _registry.FindOwnedExecution(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask owner = found.Value.Task;
        lock (owner.Gate)
        {
            return _driver.StopExecution(owner, found.Value.Execution.Id);
        }
    }

    /// <summary>终止任务：在跑的执行随之中断，未走完的步骤不再推进，之后不可恢复。</summary>
    public ErrorOr<Success> StopTask(TaskId id)
    {
        ErrorOr<WorkTask> found = _registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        ErrorOr<Success> stopped = _driver.Terminate(found.Value);
        if (!stopped.IsError)
        {
            PersistImmediately(found.Value.Id);
            _leases?.Release(found.Value.Id);
        }

        return stopped;
    }

    /// <summary>丢掉已完成或已终止的任务，删除它们的存档并放开占用。</summary>
    public IReadOnlyList<TaskId> ClearSettled()
    {
        IReadOnlyList<TaskId> cleared = _registry.ClearSettled();
        if (_records is { } coordinator)
        {
            foreach (TaskId id in cleared)
            {
                coordinator.Remove(id);
            }
        }

        if (_leases is { } leaseStore)
        {
            foreach (TaskId id in cleared)
            {
                leaseStore.Release(id);
            }
        }

        return cleared;
    }

    /// <summary>等待当前待写状态落盘，不改任务状态。</summary>
    public Task FlushAsync() => _records?.FlushAsync() ?? Task.CompletedTask;

    /// <summary>退出时先中断任务与执行并等待收口，再写全部任务存档，放开占用，最后丢弃任务域。</summary>
    public async Task ShutdownAsync()
    {
        await _driver.ShutdownAsync().ConfigureAwait(false);

        if (_records is { } coordinator)
        {
            foreach (TaskId id in _registry.Ids())
            {
                coordinator.MarkDirty(id);
            }

            await coordinator.FlushAsync().ConfigureAwait(false);
        }

        _leases?.ReleaseAll();
        _registry.Forget();
    }
}