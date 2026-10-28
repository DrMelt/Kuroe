using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>一个任务域内的任务索引：分配域内任务号、交出快照、转发执行通知。状态本身存在任务与运行期节点上。</summary>
/// <param name="executions">全部任务域共享的执行索引。</param>
/// <param name="firstTaskId">域内首个任务的标识，用户任务域按已有任务目录给出续号。</param>
public sealed class TaskRegistry(ExecutionIndex executions, int firstTaskId = 1)
{
    private readonly Lock _gate = new();
    private readonly List<WorkTask> _tasks = [];
    private int _nextTask = firstTaskId;

    /// <summary>一条值得单独提示的执行事件，宿主把它打成一行。</summary>
    public event Action<ExecutionNotice>? Notified;

    /// <summary>取本域下一个任务号。目录准备失败时任务号照常消耗，与按号命名的目录一致。</summary>
    internal TaskId Allocate()
    {
        lock (_gate)
        {
            return new TaskId(_nextTask++);
        }
    }

    /// <summary>把已定号、已备好开始端口文本与静态端口解析的任务加入本域。</summary>
    internal WorkTask Add(
        TaskId id,
        StartInputs inputs,
        IPortTexts startTexts,
        StaticPortValues staticPorts,
        CompiledFlow flow,
        string title,
        Action<string>? onText,
        Action<JournalEntry>? onEntry)
    {
        lock (_gate)
        {
            WorkTask task = new(this, id, inputs, startTexts, staticPorts, flow.Model, flow.Graph, title, onText, onEntry);
            _tasks.Add(task);

            return task;
        }
    }

    internal Execution NewExecution(WorkTask owner, ExecutionInput input) => executions.Create(owner, input);

    /// <summary>按任务号取任务，不存在时返回错误。</summary>
    public ErrorOr<WorkTask> Find(TaskId id)
    {
        lock (_gate)
        {
            WorkTask? task = _tasks.Find(candidate => candidate.Id == id);

            return task is null ? [TaskErrors.NotFound(id.Value)] : task;
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

    /// <summary>本域全部任务的快照，按提交顺序。</summary>
    public IReadOnlyList<TaskSnapshot> Snapshots()
    {
        WorkTask[] all;
        lock (_gate)
        {
            all = [.. _tasks];
        }

        return [.. all.OrderBy(task => task.Id.Value).Select(task => task.Snapshot())];
    }

    /// <summary>本域在跑的执行，含排队中的，按启动先后。</summary>
    public IReadOnlyList<ExecutionSnapshot> LiveExecutions()
    {
        WorkTask[] tasks;
        lock (_gate)
        {
            tasks = [.. _tasks];
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

    /// <summary>丢掉已收口且没有执行在跑的任务。判定与移除都在该任务 Gate 之内，保持锁序一致。</summary>
    public int ClearSettled()
    {
        WorkTask[] all;
        lock (_gate)
        {
            all = [.. _tasks];
        }

        int cleared = 0;
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

                    if (_tasks.Remove(task))
                    {
                        cleared++;
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
            tasks = [.. _tasks];
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

    internal void Report(ExecutionNotice notice) => Notified?.Invoke(notice);
}
