using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Nodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>一个任务的推进循环：激活目标去重入队后逐个评估，执行异步派发不阻塞循环，
/// 收口回调入队下游或重派目标并唤醒挂起。等待型节点的外部输入与返工意图经 Advance 与 Rework 从这里入队重新评估，
/// 队列空且任务停在等待时挂起等宿主信号。任务状态仍在任务对象上，这里是调度面。</summary>
internal sealed class TaskDriver(
    WorkTask task,
    ExecutionDispatcher dispatcher,
    IExecutionTargetResolver targetResolver)
{
    private readonly Lock _gate = new();
    private readonly Queue<NodePath> _pending = [];
    private readonly HashSet<NodePath> _queued = [];
    private readonly HashSet<NodePath> _processing = [];
    private TaskCompletionSource? _signal;
    private int _inflight;
    private bool _stop;
    private int _resumeVersion;

    /// <summary>该任务的推进循环，宿主持有以便收口。</summary>
    public Task Loop { get; private set; } = Task.CompletedTask;

    /// <summary>启动推进循环。要求持有任务 Gate：全部根执行节点入队。</summary>
    public void Start()
    {
        List<NodePath> roots;
        lock (task.Gate)
        {
            roots = [.. task.Runtime.Roots()];
        }

        Enqueue(roots);
        Loop = Task.Run(RunAsync);
    }

    /// <summary>从检查点启动推进循环：调度种子入队，任务保持暂停，恢复后由宿主决定何时推进。要求持有任务 Gate。</summary>
    public void Restore(IReadOnlyList<NodePath> seed)
    {
        Enqueue(seed);
        Loop = Task.Run(RunAsync);
    }

    /// <summary>产出已变的目标入队，由推进循环重新评估。</summary>
    public void Advance(IReadOnlyList<NodePath> targets)
    {
        Enqueue(targets);
    }

    /// <summary>此刻尚未处理的目标：排队中、已入队未取批与正在评估的节点，按首次出现顺序去重。</summary>
    public SchedulerSeed CaptureSeed()
    {
        lock (_gate)
        {
            List<NodePath> targets = [.. _pending, .. _queued, .. _processing];

            return new SchedulerSeed([.. targets.Distinct()]);
        }
    }

    /// <summary>返工语义：作废后的重派目标入队，由推进循环重新评估，重派时越过就绪判定直接重启。</summary>
    public void Rework(IReadOnlyList<TaskRuntime.ReworkTarget> targets)
    {
        Enqueue(targets.Select(target => target.Node));
    }

    /// <summary>暂停推进：在跑的执行就地中断。暂停状态在任务对象上，推进循环读取它。</summary>
    public void Pause()
    {
        foreach (Execution execution in task.Executions.Where(execution => execution.IsLive).ToArray())
        {
            execution.Interrupt("任务已暂停。");
        }
    }

    /// <summary>恢复推进：唤醒推进循环重新取待处理目标。要求持有任务 Gate。</summary>
    public void ResumeTask()
    {
        lock (_gate)
        {
            _resumeVersion++;
            _signal?.TrySetResult();
        }
    }

    /// <summary>任务当前是否暂停。暂停状态只在任务对象上，推进器只读它。</summary>
    private bool IsPaused() => task.UnderLock(() => task.Lifecycle == TaskLifecycle.Paused);

    /// <summary>终止任务：取消任务状态与在跑的执行，唤醒挂起点后主循环按停止位退出。要求持有任务 Gate。</summary>
    public ErrorOr<Success> Stop()
    {
        ErrorOr<Success> result;
        lock (task.Gate)
        {
            result = task.Terminate();
        }

        if (result.IsError)
        {
            return result;
        }

        lock (_gate)
        {
            _stop = true;
            _pending.Clear();
            _queued.Clear();
            _processing.Clear();
            _signal?.TrySetResult();
        }

        return result;
    }

    /// <summary>退出时中断推进：保留任务控制状态与节点状态，中断在跑的执行并让主循环退出。</summary>
    public void InterruptForShutdown()
    {
        Execution[] live;
        lock (task.Gate)
        {
            lock (_gate)
            {
                _stop = true;
                _pending.Clear();
                _queued.Clear();
                _processing.Clear();
                _signal?.TrySetResult();
            }

            live = [.. task.Executions.Where(execution => execution.IsLive)];
        }

        foreach (Execution execution in live)
        {
            execution.Interrupt("进程退出。");
        }
    }

    /// <summary>推进循环：取整批待激活节点逐个评估，执行异步派发不阻塞循环，新目标即时入队下一轮。
    /// 暂停时不取批，队列空且无在途派发时按停止位与任务终态退出，否则挂起等信号。</summary>
    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                List<NodePath> batch = [];
                bool paused = IsPaused();
                lock (_gate)
                {
                    if (!_stop && !paused)
                    {
                        while (_pending.TryDequeue(out NodePath node))
                        {
                            batch.Add(node);
                            _processing.Add(node);
                        }

                        _queued.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    foreach (NodePath node in batch)
                    {
                        Activate(node);
                        lock (_gate)
                        {
                            _processing.Remove(node);
                        }
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
                        done = task.State is TaskState.Done or TaskState.Terminated;
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

                bool waiting = IsPaused() && !_stop;
                int version;
                lock (_gate)
                {
                    version = _resumeVersion;
                }

                await WaitForSignalAsync(
                        waiting
                            ? () => _stop || _resumeVersion != version
                            : () => _pending.Count > 0)
                    .ConfigureAwait(false);
            }

            if (task.UnderLock(() => task.State == TaskState.Done))
            {
                lock (task.Gate)
                {
                    task.MarkCompleted();
                }

                task.NotifyCompleted();
            }
        }
        catch (Exception ex)
        {
            task.Registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的推进异常：{ex.Message}"));
        }
    }
    /// <summary>挂起推进循环，直到给定条件在锁内成立或被信号唤醒。设置信号后再判定，避免判定与挂起之间丢唤醒。</summary>
    private async Task WaitForSignalAsync(Func<bool> ready)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool proceed;
        lock (_gate)
        {
            _signal = gate;
            proceed = ready();
            if (proceed && ReferenceEquals(_signal, gate))
            {
                _signal = null;
            }
        }

        if (proceed)
        {
            return;
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



    /// <summary>一个节点收到评估意图：输入节点重新挂起等回答，审批节点按就绪停在待批准，其余按就绪条件启动一次执行。
    /// 启动的执行异步派发由收口回调处理，通知的下游即时入队。</summary>
    private void Activate(NodePath name)
    {
        bool paused = IsPaused();
        bool stopped;
        lock (_gate)
        {
            stopped = _stop;
        }

        if (stopped)
        {
            return;
        }

        if (paused)
        {
            Enqueue([name]);

            return;
        }

        List<NodePath> notify = [];
        NodeRuntime node;
        NodeInputValues? inputs;
        lock (task.Gate)
        {
            node = task.Runtime[name];
            TaskRuntime.EvaluateResult result = task.Runtime.Evaluate(node);
            notify.AddRange(result.Notify);
            inputs = result.Inputs;
        }

        foreach (NodePath next in notify)
        {
            Enqueue([next]);
        }

        // 只有派发执行的节点会给出启动输入，审批节点与输入节点不派发执行
        if (node is not RuntimeExecutable executable || inputs is null)
        {
            return;
        }

        lock (_gate)
        {
            _inflight++;
        }

        // 直接调用让装配段同步完成，执行编号与派发顺序保持确定，await 之后才回到线程池
        _ = DispatchAndSettleAsync(executable, inputs);
    }

    /// <summary>执行装配与启动的包装：收口异常上报为推进异常，不越过调度循环。</summary>
    private async Task DispatchAndSettleAsync(RuntimeExecutable node, NodeInputValues inputs)
    {
        try
        {
            await DispatchExecutionAsync(node, inputs).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            task.Registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的执行收口异常：{ex.Message}"));
        }
    }

    /// <summary>为一个执行装配、派发并收口，再按收口计划入队下游或重派目标。
    /// 无论收口结果如何必释放在途并唤醒挂起，让主循环重新判定退出或取批。</summary>
    private async Task DispatchExecutionAsync(RuntimeExecutable node, NodeInputValues inputs)
    {
        try
        {
            ExecutableNode executable = node.Executable;
            Execution? execution;
            bool paused = false;
            lock (task.Gate)
            {
                if (task.Lifecycle == TaskLifecycle.Terminated)
                {
                    node.ReleaseActive();
                    return;
                }

                ErrorOr<INodeDeclaration> declaration = targetResolver.For(executable, inputs.StaticTexts);
                if (declaration.IsError)
                {
                    node.ReleaseActive();
                    node.EnterBlocked([node.Path]);
                    task.Journal.AppendError(declaration.FirstError.Description);
                    task.Registry.Report(ExecutionNotice.From(declaration.ErrorsOrEmptyList, $"{task.Id} 停在节点 {executable.Label}"));
                    return;
                }

                execution = task.Registry.NewExecution(
                    task,
                    ExecutionInputBuilder.Build(task.Id, node, inputs, declaration.Value));
                task.Attach(execution);
                paused = task.Lifecycle == TaskLifecycle.Paused;
            }

            if (execution is null)
            {
                return;
            }

            if (paused)
            {
                execution.Interrupt("任务已暂停。");
            }

            try
            {
                await dispatcher.DispatchAsync(execution, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 派发未走完时执行不会自行收口，显式收口后走统一收口路径把执行节点停驻
                execution.MarkCanceled();
                string message = $"执行派发异常：{ex.Message}";
                lock (task.Gate)
                {
                    task.Journal.AppendError(message);
                }

                task.Registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 停在节点 {node.Executable.Label}，{message}"));
            }

            TaskRuntime.SettlePlan plan;
            bool selfActivate;
            lock (task.Gate)
            {
                node.ReleaseActive();
                plan = task.Runtime.Settle(node, execution);
                // 活跃屏障可能丢失一次来源新版本的通知：活跃释放后这里补一次评估，
                // 否则该节点停在可启动状态而无人再激活，任务会一直等待
                selfActivate = task.Runtime.CanStart(node);
            }

            if (selfActivate)
            {
                Enqueue([node.Path]);
            }

            foreach (NodePath next in plan.Notify)
            {
                Enqueue([next]);
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

    /// <summary>去重入队待激活的执行节点路径，评估幂等不会重复派发。暂停时不唤醒循环，恢复后统一取批。</summary>
    private void Enqueue(IEnumerable<NodePath> nodes)
    {
        bool paused = IsPaused();
        bool shouldWake;
        lock (_gate)
        {
            if (_stop)
            {
                return;
            }

            shouldWake = false;
            foreach (NodePath node in nodes.Where(n => _queued.Add(n)))
            {
                _pending.Enqueue(node);
                shouldWake = true;
            }

            shouldWake &= !paused;
        }

        if (shouldWake)
        {
            _signal?.TrySetResult();
        }
    }
}
