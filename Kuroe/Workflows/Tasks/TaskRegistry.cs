using ErrorOr;
using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Agent.Sessions;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Workflows.Tasks;

/// <summary>任务与 agent 的索引：分配标识、交出快照、转发执行通知。状态本身存在任务与单元上。</summary>
public sealed class TaskRegistry
{
    private readonly Lock _gate = new();
    private readonly List<AgentTask> _tasks = [];
    private readonly Dictionary<RunId, AgentRun> _runs = [];
    private int _nextTask = 1;
    private int _nextRun = 1;
    private TaskId? _active;

    /// <summary>一条值得单独提示的执行事件，宿主把它打成一行。</summary>
    public event Action<ExecutionNotice>? Notified;

    internal AgentTask Create(string goal, Workflow flow, AgentSession session, string? title)
    {
        lock (_gate)
        {
            AgentTask task = new(new TaskId(_nextTask++), goal, flow, session, title);
            _tasks.Add(task);
            _active = task.Id;

            return task;
        }
    }

    internal AgentRun NewRun(RunContext context)
    {
        lock (_gate)
        {
            AgentRun run = new(new RunId(_nextRun++), context);
            _runs[run.Id] = run;

            return run;
        }
    }

    /// <summary>前台对话发生在它身上：最近提交或被切换的任务。</summary>
    public TaskId? Active
    {
        get
        {
            lock (_gate)
            {
                return _active;
            }
        }
    }

    /// <summary>把前台对话切到该任务，任务是否存在由调用方判定。</summary>
    internal void Use(TaskId id)
    {
        lock (_gate)
        {
            _active = id;
        }
    }

    /// <summary>按任务号取任务，不存在时返回错误。</summary>
    public ErrorOr<AgentTask> Find(TaskId id)
    {
        lock (_gate)
        {
            AgentTask? task = _tasks.Find(candidate => candidate.Id == id);

            return task is null ? [TaskErrors.NotFound(id.Value)] : task;
        }
    }

    /// <summary>按 agent 号取 agent，不存在时返回错误。</summary>
    public ErrorOr<AgentRun> FindRun(RunId id)
    {
        lock (_gate)
        {
            return _runs.TryGetValue(id, out AgentRun? run) ? run : [AgentErrors.RunNotFound(id.Value)];
        }
    }

    /// <summary>全部任务的快照，按提交顺序。</summary>
    public IReadOnlyList<TaskSnapshot> Snapshots()
    {
        AgentTask[] all;
        lock (_gate)
        {
            all = [.. _tasks];
        }

        return [.. all.Select(task => task.Snapshot())];
    }

    /// <summary>在跑的 agent，含排队中的，按派出先后。</summary>
    public IReadOnlyList<RunSnapshot> LiveRuns()
    {
        AgentRun[] all;
        lock (_gate)
        {
            all = [.. _runs.Values];
        }

        return [.. all.Where(run => run.IsLive).OrderBy(run => run.Id.Value).Select(run => run.Snapshot())];
    }

    /// <summary>丢掉已收口且没有 agent 在跑的任务。判定与移除都在该任务 Gate 之内，保持锁序一致。</summary>
    public int ClearFinished()
    {
        AgentTask[] all;
        lock (_gate)
        {
            all = [.. _tasks];
        }

        int cleared = 0;
        foreach (AgentTask task in all)
        {
            lock (task.Gate)
            {
                if (!task.State.IsSettled())
                {
                    continue;
                }

                lock (_gate)
                {
                    foreach (AgentRun run in task.Runs)
                    {
                        _runs.Remove(run.Id);
                    }

                    if (_tasks.Remove(task))
                    {
                        cleared++;
                    }
                }
            }
        }

        lock (_gate)
        {
            _active = _tasks.Count == 0 ? null : _tasks[^1].Id;
        }

        return cleared;
    }

    /// <summary>丢弃全部任务与 agent 索引，退出时调用。</summary>
    internal void Forget()
    {
        lock (_gate)
        {
            _runs.Clear();
            _tasks.Clear();
            _active = null;
        }
    }

    internal void Report(ExecutionNotice notice) => Notified?.Invoke(notice);
}
