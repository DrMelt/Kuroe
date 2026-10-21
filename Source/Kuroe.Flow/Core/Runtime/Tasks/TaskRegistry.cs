using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Compiler.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务与执行的索引：分配标识、交出快照、转发执行通知。状态本身存在任务与运行期节点上。</summary>
public sealed class TaskRegistry
{
    private readonly Lock _gate = new();
    private readonly List<WorkTask> _tasks = [];
    private readonly Dictionary<ExecutionId, Execution> _executions = [];
    private int _nextTask = 1;
    private int _nextExecution = 1;

    /// <summary>一条值得单独提示的执行事件，宿主把它打成一行。</summary>
    public event Action<ExecutionNotice>? Notified;

    internal WorkTask Create(string goal, CompiledFlow flow, string? title)
    {
        lock (_gate)
        {
            WorkTask task = new(new TaskId(_nextTask++), goal, flow.Model, flow.Graph, title);
            _tasks.Add(task);

            return task;
        }
    }

    internal Execution NewExecution(ExecutionInput input)
    {
        lock (_gate)
        {
            Execution execution = new(new ExecutionId(_nextExecution++), input);
            _executions[execution.Id] = execution;

            return execution;
        }
    }

    /// <summary>按任务号取任务，不存在时返回错误。</summary>
    public ErrorOr<WorkTask> Find(TaskId id)
    {
        lock (_gate)
        {
            WorkTask? task = _tasks.Find(candidate => candidate.Id == id);

            return task is null ? [TaskErrors.NotFound(id.Value)] : task;
        }
    }

    /// <summary>按执行号取执行，不存在时返回错误。</summary>
    public ErrorOr<Execution> FindExecution(ExecutionId id)
    {
        lock (_gate)
        {
            return _executions.TryGetValue(id, out Execution? execution) ? execution : [ExecutionErrors.ExecutionNotFound(id.Value)];
        }
    }

    /// <summary>全部任务的快照，按提交顺序。</summary>
    public IReadOnlyList<TaskSnapshot> Snapshots()
    {
        WorkTask[] all;
        lock (_gate)
        {
            all = [.. _tasks];
        }

        return [.. all.Select(task => task.Snapshot())];
    }

    /// <summary>在跑的执行，含排队中的，按启动先后。</summary>
    public IReadOnlyList<ExecutionSnapshot> LiveExecutions()
    {
        Execution[] all;
        lock (_gate)
        {
            all = [.. _executions.Values];
        }

        return [.. all.Where(execution => execution.IsLive).OrderBy(execution => execution.Id.Value).Select(execution => execution.Snapshot())];
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
                if (!task.State.IsSettled())
                {
                    continue;
                }

                lock (_gate)
                {
                    foreach (Execution execution in task.Executions)
                    {
                        _executions.Remove(execution.Id);
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

    /// <summary>丢弃全部任务与执行索引，退出时调用。</summary>
    internal void Forget()
    {
        lock (_gate)
        {
            _executions.Clear();
            _tasks.Clear();
        }
    }

    internal void Report(ExecutionNotice notice) => Notified?.Invoke(notice);
}
