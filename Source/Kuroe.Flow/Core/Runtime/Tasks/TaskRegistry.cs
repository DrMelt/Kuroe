using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务索引：按任务键查找、交出快照、转发执行通知。状态本身存在任务与流程控制上。</summary>
/// <param name="executions">全部任务域共享的执行索引。</param>
/// <param name="pauseOnRestore">可恢复的中断收口时是否暂停任务。</param>
public sealed class TaskRegistry(ExecutionIndex executions, bool pauseOnRestore = true) : ITaskChangeSource
{
    private readonly Lock _gate = new();

    /// <summary>任务域是否在可恢复的中断收口时暂停任务，等待宿主恢复。</summary>
    internal bool PauseOnRestore { get; } = pauseOnRestore;

    private readonly Dictionary<string, WorkTask> _tasks = [];

    /// <summary>一条值得单独提示的执行事件，宿主把它打成一行。</summary>
    public event Action<ExecutionNotice>? Notified;

    /// <summary>任务状态已变，存档协调与观察方据此读取新状态。</summary>
    public event Action<TaskId>? Changed;

    /// <summary>把已建立的任务加入本域。</summary>
    public void Adopt(WorkTask task)
    {
        lock (_gate)
        {
            _tasks[task.Id.Value] = task;
        }
    }

    /// <summary>把恢复的执行登记进共享执行索引，并推进下一次执行号。</summary>
    public void RestoreExecution(ITaskFlowContext owner, IExecution execution) => executions.Restore(owner, execution);

    /// <summary>按任务键取任务，不存在时返回错误。</summary>
    public ErrorOr<WorkTask> Find(TaskId id)
    {
        lock (_gate)
        {
            return _tasks.TryGetValue(id.Value, out WorkTask? task)
                ? task
                : [TaskErrors.NotFound(id.Short)];
        }
    }

    /// <summary>按完整键或末尾短键取任务。短键匹配多个任务时返回歧义错误。</summary>
    public ErrorOr<WorkTask> FindByReference(string reference)
    {
        lock (_gate)
        {
            if (_tasks.TryGetValue(reference, out WorkTask? exact))
            {
                return exact;
            }

            WorkTask[] matches = [.. _tasks.Values.Where(task => task.Id.MatchesShort(reference))];

            return matches.Length switch
            {
                1 => matches[0],
                > 1 => [TaskErrors.Ambiguous(reference)],
                _ => [TaskErrors.NotFound(reference)],
            };
        }
    }

    /// <summary>按执行号取本域的执行，不存在或属于其它任务域时为空。</summary>
    public IExecution? FindExecution(ExecutionId id) =>
        FindOwnedExecution(id) is { IsError: false } found ? found.Value.Execution : null;

    /// <summary>按执行号取执行与它的归属任务。执行不存在或属于其它任务域时返回错误。</summary>
    public ErrorOr<(WorkTask Task, IExecution Execution)> FindOwnedExecution(ExecutionId id)
    {
        ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> found =
            executions.FindOwned(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        lock (_gate)
        {
            if (!_tasks.TryGetValue(found.Value.Owner.Id.Value, out WorkTask? task)
                || !ReferenceEquals(task, found.Value.Owner))
            {
                return [ExecutionErrors.ExecutionNotFound(id.Value)];
            }

            return (task, found.Value.Execution);
        }
    }

    /// <summary>本域全部任务的快照，按创建时间。</summary>
    public IReadOnlyList<TaskSnapshot> Snapshots()
    {
        WorkTask[] all;
        lock (_gate)
        {
            all = [.. _tasks.Values];
        }

        return [.. all.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id.Value).Select(task => task.Snapshot())];
    }

    /// <summary>本域在跑的执行，含排队中的，按启动先后。</summary>
    public IReadOnlyList<ExecutionSnapshot> LiveExecutions()
    {
        WorkTask[] tasks;
        lock (_gate)
        {
            tasks = [.. _tasks.Values];
        }

        return
        [
            .. tasks
                .SelectMany(task => task.UnderLock(() => task.Executions.ToArray()))
                .Where(execution => execution.IsLive)
                .OrderBy(execution => execution.Id.Value)
                .Select(execution => execution.Snapshot()),
        ];
    }

    /// <summary>丢掉已收口且没有执行在跑的任务，返回被丢掉的任务键。判定与移除都在该任务 Gate 之内，保持锁序一致。</summary>
    public IReadOnlyList<TaskId> ClearSettled()
    {
        WorkTask[] all;
        lock (_gate)
        {
            all = [.. _tasks.Values];
        }

        List<TaskId> cleared = [];
        foreach (WorkTask task in all)
        {
            lock (task.Gate)
            {
                if (!task.State.IsSettled() || task.Executions.Any(execution => execution.IsLive))
                {
                    continue;
                }

                lock (_gate)
                {
                    foreach (IExecution execution in task.Executions)
                    {
                        executions.Remove(execution.Id);
                    }

                    if (_tasks.Remove(task.Id.Value))
                    {
                        cleared.Add(task.Id);
                    }
                }
            }
        }

        return cleared;
    }

    /// <summary>丢弃本域全部任务与执行索引条目，退出时调用。</summary>
    public void Forget()
    {
        WorkTask[] tasks;
        lock (_gate)
        {
            tasks = [.. _tasks.Values];
            _tasks.Clear();
        }

        foreach (WorkTask task in tasks)
        {
            lock (task.Gate)
            {
                foreach (IExecution execution in task.Executions)
                {
                    executions.Remove(execution.Id);
                }
            }
        }
    }

    /// <summary>本域全部任务键，退出写存档时使用。</summary>
    public IReadOnlyList<TaskId> Ids()
    {
        lock (_gate)
        {
            return [.. _tasks.Keys.Select(key => new TaskId(key))];
        }
    }

    /// <summary>上报一条执行事件。</summary>
    public void Report(ExecutionNotice notice) => Notified?.Invoke(notice);

    /// <summary>上报任务状态变化。</summary>
    public void ReportChanged(TaskId id) => Changed?.Invoke(id);
}