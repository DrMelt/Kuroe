using ErrorOr;
using Kuroe.Flow.Control.Graph.Runtime.Graph;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Flow.InputChannel.Shared;

using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Control.Graph.Runtime.Nodes;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;
using Kuroe.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>一个任务的图流程控制：推进循环、恢复、重新执行、终止与等待交付。</summary>
internal sealed class GraphFlowControl(
    ITaskFlowContext task,
    GraphFlowState state,
    IExecutionTargetResolver targetResolver,
    IInputDelivery inputDelivery,
    IFlowExecutionGateway gateway) : IFlowControl, IFlowInputControl
{
    private readonly Lock _gate = new();
    private readonly Queue<NodePath> _pending = [];
    private readonly Queue<TaskRuntime.RestorePlan> _restores = [];
    private readonly HashSet<NodePath> _restoreQueued = [];
    private readonly HashSet<NodePath> _queued = [];
    private readonly HashSet<NodePath> _processing = [];

    /// <summary>未启动即中断的节点，恢复推进时重新排队。</summary>
    private readonly HashSet<NodePath> _restart = [];
    private TaskCompletionSource? _signal;
    private int _inflight;
    private bool _stop;
    private int _resumeVersion;

    /// <summary>该任务的推进循环，宿主持有以便收口。</summary>
    public Task Loop { get; private set; } = Task.CompletedTask;

    /// <summary>推进循环结束即控制实例不再需要。</summary>
    public Task Completion => Loop;

    /// <summary>是否有步骤停在阻塞。</summary>
    public bool HasBlocked => state.Runtime.HasBlocked;

    /// <summary>是否还有未走完的推进工作。</summary>
    public bool HasWork => state.Runtime.HasWork;

    /// <summary>是否有可恢复的阻塞。</summary>
    public bool HasRestores => state.Runtime.HasRestorePlans;

    /// <summary>启动推进循环。要求持有任务 Gate：全部根执行节点入队。</summary>
    public void Start()
    {
        List<NodePath> roots;
        lock (task.Gate)
        {
            roots = [.. state.Runtime.Roots()];
        }

        Enqueue(roots);
        Loop = Task.Run(RunAsync);
    }

    /// <summary>从存档启动推进循环：调度种子与当前已就绪的根节点一并入队，任务保持暂停，恢复后由宿主决定何时推进。要求持有任务 Gate。</summary>
    public void Restore(FlowSeed seed)
    {
        List<NodePath> roots;
        lock (task.Gate)
        {
            roots = [.. state.Runtime.Roots()];
        }

        Enqueue([.. seed.Steps.Select(FlowStepPaths.ToNodePath), .. roots]);
        Loop = Task.Run(RunAsync);
    }

    /// <summary>产出已变的目标入队，由推进循环重新评估。要求持有任务 Gate 之外的调用。</summary>
    public void Advance(IReadOnlyList<FlowStepPath> steps)
    {
        List<NodePath> targets;
        lock (task.Gate)
        {
            targets = [.. state.Runtime.Notify([.. steps.Select(FlowStepPaths.ToNodePath)])];
            state.Runtime.InvalidateRestores(targets);
            if (targets.Count > 0)
            {
                task.AppendInfo("上游产出变化，下游放弃恢复。");
            }

            task.Touch();
        }

        Enqueue(targets);
        task.NotifyChanged();
    }

    /// <summary>恢复推进循环，continueRestores 为真时恢复可恢复的阻塞。要求持有任务 Gate。</summary>
    public void Resume(bool continueRestores = true)
    {
        IReadOnlyList<TaskRuntime.RestorePlan> restores = continueRestores
            ? state.Runtime.PlanRestores()
            : [];
        ResumeTask(restores, clearPendingRestores: !continueRestores);
    }

    /// <summary>对阻塞的执行节点重新执行，返回实际发出的重新执行目标数。要求持有任务 Gate。</summary>
    public int Rerun()
    {
        IReadOnlyList<TaskRuntime.RerunTarget> targets;
        lock (task.Gate)
        {
            targets = state.Runtime.PlanReruns();
            foreach (IGrouping<NodePath, TaskRuntime.RerunTarget> group in targets.GroupBy(target => target.Blocked))
            {
                state.Runtime.InvalidateRestore(group.Key);
                state.Runtime.Unblock(group.Key, [.. group.Select(target => target.Node)]);
                task.AppendInfo($"重新执行节点 {group.Key}，放弃恢复。");
            }

            foreach (TaskRuntime.RerunTarget target in targets)
            {
                state.Runtime.Invalidate(target.Node);
            }

            if (targets.Count > 0)
            {
                task.Touch();
            }
        }

        if (targets.Count > 0)
        {
            Enqueue(targets.Select(target => target.Node));
            task.NotifyChanged();
        }

        return targets.Count;
    }

    /// <summary>终止任务并结束推进循环。要求持有任务 Gate。</summary>
    public ErrorOr<Success> Terminate() => Stop();

    /// <summary>此刻尚未处理的目标：排队中、已入队未取批与正在评估的节点，按首次出现顺序去重。</summary>
    public FlowSeed CaptureSeed()
    {
        lock (_gate)
        {
            List<NodePath> targets = [.. _pending, .. _queued, .. _processing, .. _restart];

            return new FlowSeed([.. targets.Distinct().Select(FlowStepPaths.From)]);
        }
    }

    /// <summary>此刻等待外部输入的步骤，按流程图顺序。</summary>
    public IReadOnlyList<FlowPendingStep> Pending()
    {
        lock (task.Gate)
        {
            return
            [
                .. inputDelivery.Accepting(state.Runtime).Select(request => new FlowPendingStep(
                    FlowStepPaths.From(request.Node),
                    state.Flow.Graph[request.Node] is ExecutableNode node && node.Type.Shape.AwaitsApproval
                        ? FlowPendingKind.Approve
                        : FlowPendingKind.Input,
                    request.Question)),
            ];
        }
    }

    /// <summary>向等待中的步骤交付一次外部输入。要求持有任务 Gate。</summary>
    public ErrorOr<Success> Deliver(FlowStepPath step, string text)
    {
        lock (task.Gate)
        {
            NodePath node = FlowStepPaths.ToNodePath(step);
            if (inputDelivery.Accepting(state.Runtime).All(request => request.Node != node))
            {
                return [Error.Validation(FlowErrorCodes.TaskInput, $"节点 {node} 未等待外部输入。")];
            }

            inputDelivery.Deliver(state.Runtime, node, text);

            return Result.Success;
        }
    }

    /// <summary>登记一次新执行到任务与流程状态。</summary>
    private GraphExecution Attach(IExecution unit, GraphExecutionInput input)
    {
        state.Bind(unit.Id, input, task.ControlVersion);
        task.AttachExecution(unit);
        unit.Started += () => task.UnderLock(() =>
        {
            state.Runtime.Executable(input.Node).RecordExecution();
            return true;
        });

        return new GraphExecution(input, unit, task.ControlVersion);
    }

    /// <summary>暂停推进：在跑的执行就地中断。暂停状态在任务对象上，推进循环读取它。</summary>
    public void Pause()
    {
        foreach (IExecution execution in task.Executions.Where(execution => execution.IsLive).ToArray())
        {
            gateway.Interrupt(execution, "任务已暂停。");
        }
    }

    /// <summary>恢复推进：唤醒推进循环重新取待处理目标，并把恢复计划按节点去重后排入队列。clearPendingRestores 为真时丢弃此前排队的计划。要求持有任务 Gate。</summary>
    public void ResumeTask(IReadOnlyList<TaskRuntime.RestorePlan>? restores = null, bool clearPendingRestores = false)
    {
        lock (_gate)
        {
            if (clearPendingRestores)
            {
                _restores.Clear();
                _restoreQueued.Clear();
            }

            EnqueueLocked(_restart);
            _restart.Clear();
            EnqueueRestoresLocked(restores ?? []);
            _resumeVersion++;
            _signal?.TrySetResult();
        }
    }

    /// <summary>排入一个收口后仍需继续的恢复计划。要求持有任务 Gate。</summary>
    private void EnqueueRestore(TaskRuntime.RestorePlan restore)
    {
        bool wake;
        lock (_gate)
        {
            if (_stop)
            {
                return;
            }

            wake = EnqueueRestoresLocked([restore]);
        }

        if (wake)
        {
            _signal?.TrySetResult();
        }
    }

    /// <summary>要求持有 <see cref="_gate"/>：恢复计划按节点去重入队，返回是否有新计划。</summary>
    private bool EnqueueRestoresLocked(IEnumerable<TaskRuntime.RestorePlan> restores)
    {
        bool added = false;
        foreach (TaskRuntime.RestorePlan restore in restores.Where(restore => _restoreQueued.Add(restore.Blocked)))
        {
            _restores.Enqueue(restore);
            added = true;
        }

        return added;
    }

    /// <summary>任务当前是否暂停。暂停状态只在任务对象上，推进器只读它。</summary>
    private bool IsPaused() => task.UnderLock(() => task.Lifecycle == TaskLifecycle.Paused);

    /// <summary>终止任务：置任务为已终止并中断在跑的执行，唤醒挂起点后主循环按停止位退出。要求持有任务 Gate。</summary>
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

        state.Runtime.Cancel();

        lock (_gate)
        {
            _stop = true;
            _pending.Clear();
            _restores.Clear();
            _restoreQueued.Clear();
            _queued.Clear();
            _processing.Clear();
            _restart.Clear();
            _signal?.TrySetResult();
        }

        return result;
    }

    /// <summary>退出时中断推进：保留任务控制状态与节点状态，中断在跑的执行并让主循环退出。</summary>
    public void InterruptForShutdown()
    {
        GraphExecution[] live;
        lock (task.Gate)
        {
            lock (_gate)
            {
                _stop = true;
                _pending.Clear();
                _restores.Clear();
                _restoreQueued.Clear();
                _queued.Clear();
                _processing.Clear();
                _signal?.TrySetResult();
            }

            live = [.. state.Executions.Where(execution => execution.IsLive)];
        }

        foreach (GraphExecution execution in live)
        {
            gateway.Interrupt(execution.Unit, "进程退出。");
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
                List<TaskRuntime.RestorePlan> restores = [];
                bool paused = IsPaused();
                lock (_gate)
                {
                    if (!_stop && !paused)
                    {
                        if (_restores.Count > 0)
                        {
                            while (_restores.TryDequeue(out TaskRuntime.RestorePlan? restore))
                            {
                                if (restore is null)
                                {
                                    continue;
                                }

                                restores.Add(restore);
                                _restoreQueued.Remove(restore.Blocked);
                            }
                        }
                        else
                        {
                            while (_pending.TryDequeue(out NodePath node))
                            {
                                batch.Add(node);
                                _processing.Add(node);
                            }

                            _queued.Clear();
                        }
                    }
                }

                if (restores.Count > 0)
                {
                    foreach (TaskRuntime.RestorePlan restore in restores)
                    {
                        lock (_gate)
                        {
                            _inflight++;
                        }

                        _ = DispatchRestoreAndSettleAsync(restore);
                    }

                    task.NotifyChanged();
                    continue;
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

                    task.NotifyChanged();
                    continue;
                }

                bool exit;
                lock (_gate)
                {
                    exit = _pending.Count == 0 && _restores.Count == 0 && _stop && _inflight == 0;
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
                        exit = _pending.Count == 0 && _restores.Count == 0 && done && _inflight == 0;
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
                            : () => _stop || _pending.Count > 0 || _restores.Count > 0)
                    .ConfigureAwait(false);
            }

            if (task.UnderLock(() => task.State == TaskState.Done))
            {
                lock (task.Gate)
                {
                    task.MarkCompleted();
                }

                task.NotifyChanged();
            }
        }
        catch (Exception ex)
        {
            string message = $"{task.Id} 的推进异常：{ex.Message}";
            lock (task.Gate)
            {
                task.AppendError(message);
            }

            task.Report(new ExecutionNotice(NoticeLevel.Warning, message));
            task.NotifyChanged();
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
        TaskRuntime.ActivationKind activation;
        NodeInputValues? inputs;
        lock (task.Gate)
        {
            node = state.Runtime[name];
            TaskRuntime.EvaluateResult result = state.Runtime.Evaluate(node);
            notify.AddRange(result.Notify);
            activation = result.Activation;
            inputs = result.Inputs;
        }

        foreach (NodePath next in notify)
        {
            Enqueue([next]);
        }

        // 只有派发执行的节点返回派发激活，审批节点、输入节点与容器直接发布
        if (node is not RuntimeExecutable executable || activation != TaskRuntime.ActivationKind.Dispatched)
        {
            return;
        }

        lock (_gate)
        {
            _inflight++;
        }

        // 直接调用让装配段同步完成，执行编号与派发顺序保持确定，await 之后才回到线程池
        _ = DispatchAndSettleAsync(executable, inputs ?? NodeInputValues.Empty);
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
            task.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的执行收口异常：{ex.Message}"));
        }
        finally
        {
            CompleteDispatch();
        }
    }

    /// <summary>恢复装配与启动的包装：收口异常上报为推进异常，不越过调度循环。</summary>
    private async Task DispatchRestoreAndSettleAsync(TaskRuntime.RestorePlan target)
    {
        try
        {
            await DispatchRestoreExecutionAsync(target).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            task.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 的执行收口异常：{ex.Message}"));
        }
        finally
        {
            CompleteDispatch();
        }
    }

    /// <summary>在途释放与挂起唤醒。无论收口结果如何都要执行。</summary>
    private void CompleteDispatch()
    {
        lock (_gate)
        {
            _inflight--;
            _signal?.TrySetResult();
        }

        task.NotifyChanged();
    }

    /// <summary>为一个执行装配、派发并收口，再按收口计划入队下游或重派目标。</summary>
    private async Task DispatchExecutionAsync(RuntimeExecutable node, NodeInputValues inputs)
    {
        ExecutableNode executable = node.Executable;
        (GraphExecution Execution, bool Paused)? prepared;
        lock (task.Gate)
        {
            if (task.Lifecycle == TaskLifecycle.Terminated)
            {
                node.ReleaseActive();
                return;
            }

            ErrorOr<INodeDeclaration> declaration = targetResolver.For(executable, inputs);
            if (declaration.IsError)
            {
                node.ReleaseActive();
                string reason = string.IsNullOrWhiteSpace(declaration.FirstError.Description)
                    ? "执行目标解析失败。"
                    : declaration.FirstError.Description;
                node.EnterBlocked([node.Path], new NodeBlock(reason, null, DateTimeOffset.UtcNow));
                task.AppendError(reason);
                task.Report(ExecutionNotice.From(declaration.ErrorsOrEmptyList, $"{task.Id} 停在节点 {executable.Label}"));
                return;
            }

            GraphExecutionInput input = GraphExecutionInputBuilder.Build(task.Id, node, inputs, declaration.Value, state.Flow.Graph);
            GraphExecution execution = Attach(gateway.Open(task, GraphExecution.BuildRequest(input)), input);
            prepared = (execution, task.Lifecycle == TaskLifecycle.Paused);
        }

        if (prepared is not { } step)
        {
            return;
        }

        if (step.Paused)
        {
            gateway.Interrupt(step.Execution.Unit, "任务已暂停。");
        }

        await RunAndSettleAsync(node, step.Execution, null).ConfigureAwait(false);
    }

    /// <summary>按恢复计划装配一次恢复执行，派发并收口。</summary>
    private async Task DispatchRestoreExecutionAsync(TaskRuntime.RestorePlan target)
    {
        (RuntimeExecutable Node, GraphExecution Execution, TaskRuntime.RestorePlan Restore)? prepared;
        lock (task.Gate)
        {
            RuntimeExecutable node = state.Runtime.Executable(target.Blocked);
            if (task.Lifecycle is TaskLifecycle.Terminated or TaskLifecycle.Paused
                || node.Block is null
                || !state.Runtime.IsCurrentRestorePlan(target))
            {
                return;
            }

            state.Runtime.RemoveRestorePlan(target);
            GraphExecution execution = Attach(
                gateway.Open(task, GraphExecution.BuildRequest(target.Input)),
                target.Input);
            node.ClearRerun();
            node.ClearBlock();
            node.AddActive();
            task.AppendInfo(
                $"从执行 {target.Source.Id.Value} 恢复为执行 {execution.Id.Value}，节点 {node.Path}。");
            prepared = (node, execution, target);
        }

        if (prepared is not { } step)
        {
            return;
        }

        await RunAndSettleAsync(step.Node, step.Execution, step.Restore).ConfigureAwait(false);
    }

    /// <summary>派发一个已装配的执行并收口，再按收口计划入队下游或重派目标。</summary>
    private async Task RunAndSettleAsync(
        RuntimeExecutable node,
        GraphExecution execution,
        TaskRuntime.RestorePlan? restore)
    {
        ErrorOr<Success> run;
        try
        {
            run = restore is null
                ? await gateway.RunAsync(
                    execution.Unit,
                    node.Executable.Type as IExecutionRunnerFactory,
                    execution.Target).ConfigureAwait(false)
                : await gateway.RunRestoreAsync(
                    execution.Unit,
                    restore.Source.Unit,
                    restore.Prepared).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            run = Error.Failure("Task.Dispatch", $"执行派发异常：{ex.Message}");
        }

        if (run.IsError)
        {
            string message = run.FirstError.Description;
            lock (task.Gate)
            {
                task.AppendError(message);
            }

            task.Report(new ExecutionNotice(NoticeLevel.Warning, $"{task.Id} 停在节点 {node.Executable.Label}，{message}"));
        }

        TaskRuntime.SettlePlan plan;
        TaskRuntime.RestorePlan? autoRestore = null;
        bool selfActivate;
        lock (task.Gate)
        {
            node.ReleaseActive();
            plan = state.Runtime.Settle(node, execution);
            if (plan.Interruption == ExecutionInterruption.Restorable && plan.Restore is { } restorable)
            {
                if (task.Lifecycle == TaskLifecycle.Paused)
                {
                    // 任务已经暂停，恢复计划留给用户恢复时派发
                }
                else if (task.PauseOnRestore && execution.ControlVersion == task.ControlVersion)
                {
                    task.PauseForRestore();
                }
                else
                {
                    // 控制动作已发生在这次执行创建之后，保持运行并直接继续恢复
                    autoRestore = restorable;
                }
            }

            if (plan.Interruption == ExecutionInterruption.NotStarted
                && task.Lifecycle != TaskLifecycle.Terminated)
            {
                node.RequestRerun();
                lock (_gate)
                {
                    _restart.Add(node.Path);
                }
            }

            // 活跃屏障可能丢失一次来源新版本的通知：活跃释放后这里补一次评估，
            // 否则该节点停在可启动状态而无人再激活，任务会一直等待
            selfActivate = state.Runtime.CanStart(node);
        }

        if (autoRestore is { } restorePlan)
        {
            EnqueueRestore(restorePlan);
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

            shouldWake = EnqueueLocked(nodes);
            shouldWake &= !paused;
        }

        if (shouldWake)
        {
            _signal?.TrySetResult();
        }
    }

    /// <summary>要求持有 <see cref="_gate"/>：去重入队待激活节点，返回是否有新目标。</summary>
    private bool EnqueueLocked(IEnumerable<NodePath> nodes)
    {
        bool added = false;
        foreach (NodePath node in nodes.Where(node => _queued.Add(node)))
        {
            _pending.Enqueue(node);
            added = true;
        }

        return added;
    }
}
