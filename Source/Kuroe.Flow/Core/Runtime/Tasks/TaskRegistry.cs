using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务索引：按任务键查找、交出快照、转发执行通知。状态本身存在任务与运行期节点上。</summary>
/// <param name="executions">全部任务域共享的执行索引。</param>
public sealed class TaskRegistry(ExecutionIndex executions)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, WorkTask> _tasks = [];

    /// <summary>一条值得单独提示的执行事件，宿主把它打成一行。</summary>
    public event Action<ExecutionNotice>? Notified;

    /// <summary>一个任务走完的通知，检查点协调据此写终态。</summary>
    internal event Action<WorkTask>? Completed;

    /// <summary>把已定键、已备好开始端口文本的任务加入本域。</summary>
    internal WorkTask Add(
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        CompiledFlow flow,
        string title,
        Action<string>? onText,
        Action<JournalEntry>? onEntry)
    {
        lock (_gate)
        {
            WorkTask task = new(this, id, inputs, startTexts, flow.Model, flow.Graph, title, onText, onEntry);
            _tasks[task.Id.Value] = task;

            return task;
        }
    }

    /// <summary>把已恢复的任务加入本域。任务键已由检查点给出。</summary>
    internal void Adopt(WorkTask task)
    {
        lock (_gate)
        {
            _tasks[task.Id.Value] = task;
        }
    }

    /// <summary>把恢复的执行登记进共享执行索引，并推进下一次执行号。</summary>
    internal void RestoreExecution(WorkTask owner, Execution execution) => executions.Restore(owner, execution);

    internal Execution NewExecution(WorkTask owner, ExecutionInput input) => executions.Create(owner, input);

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

    /// <summary>按执行号取本域的执行，不存在或属于其它任务域时返回错误。</summary>
    public ErrorOr<Execution> FindExecution(ExecutionId id)
    {
        if (executions.FindOwned(id) is not { IsError: false } found
            || !ReferenceEquals(found.Value.Owner.Registry, this))
        {
            return [ExecutionErrors.ExecutionNotFound(id.Value)];
        }

        return found.Value.Execution;
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
                    foreach (Execution execution in task.Executions)
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
    internal void Forget()
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
                foreach (Execution execution in task.Executions)
                {
                    executions.Remove(execution.Id);
                }
            }
        }
    }

    /// <summary>本域全部任务键，退出写检查点时使用。</summary>
    internal IReadOnlyList<TaskId> Ids()
    {
        lock (_gate)
        {
            return [.. _tasks.Keys.Select(key => new TaskId(key))];
        }
    }

    internal void Report(ExecutionNotice notice) => Notified?.Invoke(notice);

    internal void ReportCompleted(WorkTask task) => Completed?.Invoke(task);
}
