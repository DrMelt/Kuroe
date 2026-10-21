using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Nodes;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>一个任务的推进循环：激活目标去重入队后逐个评估，执行异步派发不阻塞循环，
/// 收口回调入队下游或重派目标并唤醒挂起。等待型节点的外部输入与返工意图经 Resume 与 Rework 从这里入队重新评估，
/// 队列空且任务停在等待时挂起等宿主信号。任务状态仍在任务对象上，这里是调度面。</summary>
internal sealed class TaskDriver(
    WorkTask task,
    TaskRegistry registry,
    ExecutionDispatcher dispatcher,
    IExecutionTargetResolver targetResolver)
{
    private readonly Lock _gate = new();
    private readonly Queue<NodePath> _pending = [];
    private readonly HashSet<NodePath> _queued = [];
    private TaskCompletionSource? _signal;
    private int _inflight;
    private bool _stop;

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

    /// <summary>产出已变的目标入队，由推进循环重新评估。</summary>
    public void Resume(IReadOnlyList<NodePath> targets)
    {
        Enqueue(targets);
    }

    /// <summary>返工语义：作废后的重派目标入队，由推进循环重新评估，重派时越过就绪判定直接重启。</summary>
    public void Rework(IReadOnlyList<TaskRuntime.ReworkTarget> targets)
    {
        Enqueue(targets.Select(target => target.Node));
    }

    /// <summary>停止推进循环：取消任务停掉全部执行，唤醒挂起点后主循环按停止位退出。</summary>
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

    /// <summary>推进循环：取整批待激活节点逐个评估，执行异步派发不阻塞循环，新目标即时入队下一轮。
    /// 队列空且无在途派发时按停止位与任务终态退出，否则挂起等信号。</summary>
    private async Task RunAsync()
    {
        try
        {
            while (true)
            {
                List<NodePath> batch = [];
                lock (_gate)
                {
                    while (_pending.TryDequeue(out NodePath node))
                    {
                        batch.Add(node);
                    }

                    _queued.Clear();
                }

                if (batch.Count > 0)
                {
                    foreach (NodePath node in batch)
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
                    // 停止位单独不足挂起：在途执行仍多，收口回调会逐个唤醒，无需空转
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

    /// <summary>一个节点收到评估意图：输入节点重新挂起等回答，审批节点按就绪停在待批准，其余按就绪条件启动一次执行。
    /// 启动的执行异步派发由收口回调处理，通知的下游即时入队。</summary>
    private void Activate(NodePath name)
    {
        List<NodePath> notify = [];
        bool started;
        NodeRuntime node;
        lock (task.Gate)
        {
            node = task.Runtime[name];
            TaskRuntime.EvaluateResult result = task.Runtime.Evaluate(node);
            notify.AddRange(result.Notify);
            started = result.Started;
        }

        foreach (NodePath next in notify)
        {
            Enqueue([next]);
        }

        // 只有派发执行的节点会给出启动项，审批节点与输入节点不派发执行
        if (node is not RuntimeExecutable executable || !started)
        {
            return;
        }

        lock (_gate)
        {
            _inflight++;
        }

        // 直接调用让装配段同步完成，执行编号与派发顺序保持确定，await 之后才回到线程池
        _ = DispatchAndSettleAsync(executable);
    }

    /// <summary>执行装配与启动的包装：收口异常上报为推进异常，不越过调度循环。</summary>
    private async Task DispatchAndSettleAsync(RuntimeExecutable node)
    {
        try
        {
            await DispatchExecutionAsync(node).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的执行收口异常：{ex.Message}"));
        }
    }

    /// <summary>为一个执行装配、派发并收口，再按收口计划入队下游或重派目标。
    /// 无论收口结果如何必释放在途并唤醒挂起，让主循环重新判定退出或取批。</summary>
    private async Task DispatchExecutionAsync(RuntimeExecutable node)
    {
        try
        {
            ExecutableNode executable = node.Executable;
            Execution? execution;
            lock (task.Gate)
            {
                if (task.State == TaskState.Canceled)
                {
                    node.ReleaseActive();
                    return;
                }

                ErrorOr<INodeDeclaration> declaration = targetResolver.For(executable);
                if (declaration.IsError)
                {
                    node.ReleaseActive();
                    node.EnterBlocked([node.Path]);
                    task.Journal.Append(new ErrorEntry(declaration.FirstError.Description));
                    registry.Report(ExecutionNotice.From(declaration.ErrorsOrEmptyList, $"{task.Id} 停在节点 {executable.Label}"));
                    return;
                }

                execution = registry.NewExecution(ExecutionInputBuilder.Build(task.Id, node, task.Context, declaration.Value));
                task.Attach(execution);
            }

            if (execution is null)
            {
                return;
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
                    task.Journal.Append(new ErrorEntry(message));
                }

                registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 停在节点 {node.Executable.Label}，{message}"));
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

    /// <summary>去重入队待激活的执行节点路径，评估幂等不会重复派发。</summary>
    private void Enqueue(IEnumerable<NodePath> nodes)
    {
        bool shouldWake;
        lock (_gate)
        {
            shouldWake = false;
            foreach (NodePath node in nodes.Where(n => _queued.Add(n)))
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
