using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows;
using Kuroe.Workflows.Tasks;
using Run = Kuroe.Executions.Runs.Run;
using ExecutableNode = Kuroe.Shared.Workflows.Graph.ExecutableNode;

namespace Kuroe.Workflows.Engine;

/// <summary>一个任务的推进循环：激活目标去重入队后逐个评估，run 异步派发不阻塞循环，
/// 收口回调入队下游或重派目标并唤醒挂起。批准与返工意图从这里入队重新评估，
/// 队列空且任务停在等待时挂起等宿主信号。任务状态仍在任务对象上，这里是调度面。</summary>
internal sealed class TaskDriver(
    WorkTask task,
    TaskRegistry registry,
    RunDispatcher dispatcher,
    NodeModelResolver models)
{
    private readonly Lock _gate = new();
    private readonly Queue<int> _pending = [];
    private readonly HashSet<int> _queued = [];
    private TaskCompletionSource? _signal;
    private int _inflight;
    private bool _stop;

    /// <summary>该任务的推进循环，宿主持有以便收口。</summary>
    public Task Loop { get; private set; } = Task.CompletedTask;

    /// <summary>启动推进循环。要求持有任务 Gate：全部根执行节点入队。</summary>
    public void Start()
    {
        List<int> roots;
        lock (task.Gate)
        {
            roots = [.. task.Runtime.Roots()];
        }

        Enqueue(roots);
        Loop = Task.Run(RunAsync);
    }

    /// <summary>批准语义：清掉指定 run 的等待，等待清空的节点放行下游并刷新容器，再连同待批准容器入队进入下一步评估。
    /// runs 为空时批准全部等待。</summary>
    public void Approve(IReadOnlyList<RunId> runs)
    {
        List<int> targets;
        lock (task.Gate)
        {
            targets = [.. task.Runtime.ApproveRuns(runs)];
            if (runs.Count == 0)
            {
                targets.AddRange(task.Runtime.ReleaseContainers());
            }
        }

        Enqueue(targets.Distinct());
    }

    /// <summary>返工语义：作废后的重派目标入队，由推进循环重新评估，重派时越过就绪判定直接重启。</summary>
    public void Rework(IReadOnlyList<(int Blocked, int Node, int? Item)> targets)
    {
        Enqueue(targets.Select(target => target.Node));
    }

    /// <summary>回答语义：回答已产出的输入节点要放行的下游入队，由推进循环重新评估。</summary>
    public void Answer(IReadOnlyList<int> targets)
    {
        Enqueue(targets);
    }

    /// <summary>停止推进循环：取消任务停掉全部 run，唤醒挂起点后主循环按停止位退出。</summary>
    public void Stop()
    {
        lock (task.Gate)
        {
            task.Cancel();
        }

        lock (_gate)
        {
            _stop = true;
            _signal?.TrySetResult();
        }
    }

    /// <summary>推进循环：取整批待激活节点逐个评估，run 异步派发不阻塞循环，新目标即时入队下一轮。
    /// 队列空且无在途派发时按停止位与任务终态退出，否则挂起等信号。</summary>
    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                List<int> batch = [];
                lock (_gate)
                {
                    while (_pending.TryDequeue(out int node))
                    {
                        batch.Add(node);
                    }

                    _queued.Clear();
                }

                if (batch.Count > 0)
                {
                    foreach (int node in batch)
                    {
                        Activate(node);
                    }

                    continue;
                }

                bool exit;
                lock (_gate)
                {
                    exit = _pending.Count == 0 && _stop && _inflight == 0;
                }

                if (!exit)
                {
                    bool done;
                    lock (task.Gate)
                    {
                        done = task.State is TaskState.Done or TaskState.Canceled;
                    }

                    lock (_gate)
                    {
                        exit = _pending.Count == 0 && done && _inflight == 0;
                    }
                }

                if (exit)
                {
                    break;
                }

                var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                bool recheck;
                lock (_gate)
                {
                    // 挂起判定与入队之间可能又有目标，队列非空则不去挂起重新取批。
                    // 停止位单独不足挂起：在途 run 仍多，收口回调会逐个唤醒，无需空转
                    recheck = _pending.Count > 0;
                    if (!recheck)
                    {
                        _signal = gate;
                    }
                }

                if (recheck)
                {
                    continue;
                }

                await gate.Task.ConfigureAwait(false);
                lock (_gate)
                {
                    if (ReferenceEquals(_signal, gate))
                    {
                        _signal = null;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的推进异常：{ex.Message}"));
        }
    }

    /// <summary>一个执行节点收到评估意图：静态拆分就地产出、放行待批准的节点、输入齐备启动实例。
    /// 启动的 run 异步派发由收口回调处理，通知的下游即时入队。</summary>
    private void Activate(int nodeIndex)
    {
        List<int> notify = [];
        List<TaskRuntime.RunStarter> starters = [];
        RuntimeExecutable node;
        lock (task.Gate)
        {
            node = task.Runtime.Executable(nodeIndex);
            TaskRuntime.EvaluateResult result = task.Runtime.Evaluate(node);
            notify.AddRange(result.Notify);
            starters.AddRange(result.Starters);
        }

        foreach (int next in notify)
        {
            Enqueue([next]);
        }

        foreach (TaskRuntime.RunStarter starter in starters)
        {
            lock (_gate)
            {
                _inflight++;
            }

            // 直接调用让装配段按 starter 顺序同步完成，run 编号与派发顺序保持确定，await 之后才回到线程池
            _ = DispatchAndSettleAsync(node, starter);
        }
    }

    /// <summary>run 装配与启动的包装：收口异常上报为推进异常，不越过调度循环。</summary>
    private async Task DispatchAndSettleAsync(RuntimeExecutable node, TaskRuntime.RunStarter starter)
    {
        try
        {
            await DispatchRunAsync(node, starter).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的 run 收口异常：{ex.Message}"));
        }
    }

    /// <summary>为一个实例装配、派发并收口，再按收口计划入队下游或重派目标。
    /// 无论收口结果如何必释放在途并唤醒挂起，让主循环重新判定退出或取批。</summary>
    private async Task DispatchRunAsync(RuntimeExecutable node, TaskRuntime.RunStarter starter)
    {
        try
        {
            ExecutableNode executable = node.Executable;
            Run? run;
            lock (task.Gate)
            {
                if (task.State == TaskState.Canceled)
                {
                    node.ReleaseActive();
                    return;
                }

                ErrorOr<string> model = models.For(executable);
                if (model.IsError)
                {
                    node.ReleaseActive();
                    node.EnterBlocked([(node.Index, null)]);
                    task.Journal.Append(new ErrorEntry(model.FirstError.Description));
                    registry.Report(ExecutionNotice.From(model.ErrorsOrEmptyList, $"{task.Id} 停在节点 {executable.Name}"));
                    return;
                }

                run = registry.NewRun(RunContextFactory.Create(task, node, starter.Item, model.Value));
                task.Attach(run);
            }

            if (run is null)
            {
                return;
            }

            try
            {
                await dispatcher.DispatchAsync(run, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 派发未走完时 run 不会自行收口，显式收口后走统一收口路径把实例停驻
                run.MarkCanceled();
                string message = $"run 派发异常：{ex.Message}";
                lock (task.Gate)
                {
                    task.Journal.Append(new ErrorEntry(message));
                }

                registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 停在节点 {node.Executable.Name}，{message}"));
            }

            TaskRuntime.SettlePlan plan;
            bool selfActivate;
            lock (task.Gate)
            {
                node.ReleaseActive();
                plan = task.Runtime.Settle(run);
                // 活跃屏障可能丢失一次来源新版本的通知：活跃释放后这里补一次评估，
                // 否则该节点停在可启动状态而无人再激活，任务会一直等待
                selfActivate = task.Runtime.CanStart(node);
            }

            if (selfActivate)
            {
                Enqueue([node.Index]);
            }

            foreach (int next in plan.Notify)
            {
                Enqueue([next]);
            }

            foreach ((int nodeIndex, int? _) in plan.Rerun)
            {
                Enqueue([nodeIndex]);
            }
        }
        finally
        {
            lock (_gate)
            {
                _inflight--;
                _signal?.TrySetResult();
            }
        }
    }

    /// <summary>去重入队待激活的执行节点序号，评估幂等不会重复派发。</summary>
    private void Enqueue(IEnumerable<int> nodes)
    {
        bool shouldWake;
        lock (_gate)
        {
            shouldWake = false;
            foreach (int node in nodes.Where(n => _queued.Add(n)))
            {
                _pending.Enqueue(node);
                shouldWake = true;
            }
        }

        if (shouldWake)
        {
            _signal?.TrySetResult();
        }
    }
}
