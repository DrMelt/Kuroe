using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Tasks.Shared;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Maf.Control;

/// <summary>一个任务的 MAF 流程控制：MAF 运行期决定步骤推进，Kuroe 执行单元承担每次运行。</summary>
internal sealed class MafFlowControl(
    ITaskFlowContext task,
    ITaskExecutionGateway gateway,
    MafFlowState state) : IFlowControl, IFlowRerun
{
    private readonly MafFlowCheckpoints _checkpoints = new(task, state);
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource? _cancel;
    private string? _interruptReason;
    private int _run;
    private bool _running;
    private bool _completed;
    private volatile bool _paused;
    private volatile bool _stopped;
    private volatile bool _failed;
    private volatile bool _interrupted;

    /// <summary>该任务的推进循环，宿主持有以便收口。</summary>
    public Task Loop { get; private set; } = Task.CompletedTask;

    /// <summary>终止、退出中断或流程走完且推进循环退出后完成。</summary>
    public Task Completion => _completion.Task;

    /// <summary>流程推进失败后停在阻塞。</summary>
    public bool HasBlocked => _failed;

    /// <summary>是否还有未走完的步骤。</summary>
    public bool HasWork => !_failed && !_stopped && state.PendingSteps.Count > 0;

    /// <summary>MAF 流程不保留可恢复的阻塞，中断恢复从入口重新执行。</summary>
    public bool HasRestores => false;

    /// <summary>启动推进循环。要求持有任务 Gate。</summary>
    public void Start()
    {
        lock (task.Gate)
        {
            state.PendingSteps.Clear();
            state.PendingSteps.AddRange(state.Definition.Steps.Select(step => step.Id));
            _paused = false;
            Launch();
        }
    }

    /// <summary>从存档恢复流程，任务保持暂停，恢复后由宿主决定何时推进。要求持有任务 Gate。</summary>
    public void Restore(FlowSeed seed)
    {
        lock (task.Gate)
        {
            state.PendingSteps.Clear();
            state.PendingSteps.AddRange(seed.Steps
                .Select(path => state.Definition.Find(path.Value))
                .OfType<MafStep>()
                .Select(step => step.Id));
            _paused = true;
        }
    }

    /// <summary>暂停推进并中断在跑执行。要求持有任务 Gate。</summary>
    public void Pause()
    {
        CancellationTokenSource? cancel;
        lock (task.Gate)
        {
            _paused = true;
            cancel = _cancel;
        }

        Cancel(cancel);
        InterruptLive("任务已暂停。");
    }

    /// <summary>恢复推进循环；MAF 流程没有可恢复的阻塞，continueRestores 不参与。要求持有任务 Gate。</summary>
    public void Resume(bool continueRestores = true)
    {
        lock (task.Gate)
        {
            _paused = false;
            Launch();
        }
    }

    /// <summary>终止任务并结束推进循环。要求持有任务 Gate。</summary>
    public ErrorOr<Success> Terminate()
    {
        CancellationTokenSource? cancel;
        ErrorOr<Success> result;
        lock (task.Gate)
        {
            _stopped = true;
            cancel = _cancel;
            result = task.Terminate();
        }

        Cancel(cancel);
        CompleteWhenLoopEnds();

        return result;
    }

    /// <summary>放弃检查点与当前运行，从入口重新执行流程，返回发出的重新执行目标数。要求持有任务 Gate。</summary>
    public int Rerun()
    {
        CancellationTokenSource? cancel;
        lock (task.Gate)
        {
            if (!_failed || _stopped || _completed)
            {
                return 0;
            }

            _failed = false;
            _interrupted = false;
            _paused = false;
            state.FreezeCheckpoint = false;
            state.Checkpoint = null;
            state.PendingSteps.Clear();
            state.PendingSteps.AddRange(state.Definition.Steps.Select(step => step.Id));
            cancel = _cancel;
            task.AppendInfo("MAF 流程从入口重新执行。");
        }

        Cancel(cancel);
        lock (task.Gate)
        {
            Launch();
        }

        return 1;
    }

    /// <summary>尚未走完的步骤，供存档捕获。</summary>
    public FlowSeed CaptureSeed()
    {
        lock (task.Gate)
        {
            return new FlowSeed([.. state.PendingSteps.Select(step => step.ToPath())]);
        }
    }

    /// <summary>退出时中断推进，保留任务控制状态与步骤状态。</summary>
    public void InterruptForShutdown()
    {
        CancellationTokenSource? cancel;
        lock (task.Gate)
        {
            _stopped = true;
            cancel = _cancel;
            if (task.Executions.Any(execution => execution.IsLive))
            {
                state.FreezeCheckpoint = true;
                state.Checkpoint = null;
                state.PendingSteps.Clear();
                state.PendingSteps.AddRange(state.Definition.Steps.Select(step => step.Id));
            }
        }

        Cancel(cancel);
        InterruptLive("进程退出。");
        CompleteWhenLoopEnds();
    }

    /// <summary>中断在跑的执行。</summary>
    private void InterruptLive(string reason)
    {
        foreach (IExecution execution in task.Executions.Where(execution => execution.IsLive).ToArray())
        {
            gateway.Interrupt(execution, reason);
        }
    }

    /// <summary>在没有循环推进时启动一次推进循环。要求持有任务 Gate。</summary>
    private void Launch()
    {
        if (_running || _stopped || _failed || _completed || _paused)
        {
            return;
        }

        _running = true;
        Loop = Task.Run(RunAsync, CancellationToken.None);
    }

    /// <summary>取消运行与事件流；暂停、终止、重跑与中断都在它之外收口运行。</summary>
    private static void Cancel(CancellationTokenSource? cancel)
    {
        try
        {
            cancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 推进循环已经释放取消源。
        }
    }

    /// <summary>释放已结束的运行；释放失败不影响任务状态。</summary>
    private static async Task DisposeRunAsync(IMafWorkflowRun? run)
    {
        if (run is null)
        {
            return;
        }

        try
        {
            await run.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 运行已经终止，释放失败不影响任务状态。
        }
    }

    /// <summary>等推进循环退出后完成控制实例。</summary>
    private void CompleteWhenLoopEnds()
    {
        Task loop;
        lock (task.Gate)
        {
            loop = Loop;
        }

        _ = AwaitLoopAsync(loop);
    }

    private async Task AwaitLoopAsync(Task loop)
    {
        await loop.ConfigureAwait(false);
        _completion.TrySetResult();
    }

    /// <summary>推进循环：由 MAF 运行期驱动步骤，事件流结束视为流程走完。</summary>
    private async Task RunAsync()
    {
        using CancellationTokenSource cancel = new();
        int run;
        int startVersion;
        lock (task.Gate)
        {
            if (_stopped || _failed || _completed || _paused)
            {
                _running = false;
                return;
            }

            _interrupted = false;
            _interruptReason = null;
            state.FreezeCheckpoint = false;
            run = ++_run;
            startVersion = task.ControlVersion;
            _cancel = cancel;
        }

        IMafWorkflowRun? runHandle = null;
        try
        {
            runHandle = await MafWorkflowRunner.StartAsync(
                state.Definition,
                Entry(),
                task.Id.Value,
                step => Handler(run, step),
                _checkpoints,
                cancel.Token).ConfigureAwait(false);

            await foreach (MafWorkflowEvent update in runHandle.WatchAsync(cancel.Token).ConfigureAwait(false))
            {
                Handle(update);
            }

            if (!IsCurrent(run))
            {
                return;
            }

            if (_interrupted)
            {
                SettleInterruption(run, startVersion);
            }
            else if (!_stopped && !_paused && !_failed)
            {
                Complete(run);
            }
        }
        catch (OperationCanceledException)
        {
            // 暂停与终止取消运行属于正常路径。
            if (_interrupted && IsCurrent(run))
            {
                SettleInterruption(run, startVersion);
            }
        }
        catch (Exception ex)
        {
            if (!IsCurrent(run))
            {
                return;
            }

            if (_interrupted)
            {
                SettleInterruption(run, startVersion);
            }
            else if (!_stopped)
            {
                Fail(run, ex);
            }
        }
        finally
        {
            // 旧运行释放完成前保持推进循环占用，避免恢复启动的运行与旧运行重叠。
            await DisposeRunAsync(runHandle).ConfigureAwait(false);

            lock (task.Gate)
            {
                if (run == _run)
                {
                    _cancel = null;
                    _running = false;
                    Launch();
                    if (_completed || _stopped)
                    {
                        _completion.TrySetResult();
                    }
                }
            }
        }
    }

    /// <summary>该运行是否仍是当前推进循环。</summary>
    private bool IsCurrent(int run)
    {
        lock (task.Gate)
        {
            return run == _run;
        }
    }

    /// <summary>入口消息：开始端口文本作为第一步的上游产出。</summary>
    private MafStepMessage Entry()
    {
        MafStep entry = state.Definition.Entry().Value;

        return new MafStepMessage(
            entry.Id,
            [.. state.StartTexts.Texts
                .Where(pair => pair.Key != MafFlowDefinition.ModelPort)
                .Select(pair => new MafStepOutput(entry.Id, pair.Key, pair.Value))]);
    }

    /// <summary>把一个步骤接到 Kuroe 执行网关，中断上报只对发起它的运行有效。</summary>
    private MafStepHandler Handler(int run, MafStep step) =>
        (message, cancellationToken) =>
            new MafStepExecutor(task, gateway, step, state.Model, new RunControl(this, run))
                .HandleAsync(message, cancellationToken);

    /// <summary>流程是否已停止接收新的步骤运行。</summary>
    private bool Halted => _stopped || _paused || _interrupted;

    /// <summary>本次运行是否已停止推进，过期的运行一律视为停止。</summary>
    private bool HaltedFor(int run)
    {
        lock (task.Gate)
        {
            return run != _run || Halted;
        }
    }

    /// <summary>步骤执行被中断：放弃检查点，停止当前运行，交给推进循环收口。</summary>
    private void Interrupt(int run, string reason)
    {
        CancellationTokenSource? cancel;
        lock (task.Gate)
        {
            if (run != _run || _stopped)
            {
                return;
            }

            _interrupted = true;
            _interruptReason = reason;
            state.FreezeCheckpoint = true;
            state.Checkpoint = null;
            state.PendingSteps.Clear();
            state.PendingSteps.AddRange(state.Definition.Steps.Select(step => step.Id));
            cancel = _cancel;
        }

        Cancel(cancel);
        task.NotifyChanged();
    }

    /// <summary>把中断上报与停止判定绑定到建立它的运行。</summary>
    private sealed class RunControl(MafFlowControl owner, int run) : IMafStepRunControl
    {
        /// <inheritdoc/>
        public bool Halted => owner.HaltedFor(run);

        /// <inheritdoc/>
        public void Interrupt(string reason) => owner.Interrupt(run, reason);
    }

    /// <summary>中断收口：控制动作发生在这次运行期间时保留推进，否则把任务停在可恢复阻塞。</summary>
    private void SettleInterruption(int run, int startVersion)
    {
        if (_stopped)
        {
            return;
        }

        lock (task.Gate)
        {
            if (run != _run || task.Lifecycle != TaskLifecycle.Active || task.ControlVersion != startVersion)
            {
                return;
            }

            _paused = true;
            task.PauseForRestore();
            task.AppendInfo($"MAF 步骤执行被中断，任务停在可恢复阻塞：{_interruptReason}");
        }

        task.NotifyChanged();
    }

    /// <summary>把运行事件落到任务过程记录与步骤状态。</summary>
    private void Handle(MafWorkflowEvent update)
    {
        switch (update)
        {
            case MafStepStarted started:
                lock (task.Gate)
                {
                    state.PendingSteps.Remove(started.Step.Id);
                    task.AppendInfo($"MAF 步骤 {started.Step.Label} 开始。");
                    task.Touch();
                }

                task.NotifyChanged();
                break;

            case MafStepCompleted completed:
                lock (task.Gate)
                {
                    task.AppendInfo($"MAF 步骤 {completed.Step.Label} 完成。");
                    task.Touch();
                }

                task.NotifyChanged();
                break;

            case MafStepFailed failed:
                lock (task.Gate)
                {
                    if (Halted)
                    {
                        return;
                    }

                    _failed = true;
                    task.AppendError($"MAF 步骤 {failed.Step.Label} 失败：{failed.Reason}");
                }

                task.NotifyChanged();
                break;

            case MafWorkflowFailed error:
                lock (task.Gate)
                {
                    if (Halted)
                    {
                        return;
                    }

                    _failed = true;
                    task.AppendError($"MAF 流程错误：{error.Reason}");
                }

                task.NotifyChanged();
                break;
        }
    }

    /// <summary>流程走完：清空待处理步骤并标记任务完成。</summary>
    private void Complete(int run)
    {
        lock (task.Gate)
        {
            if (run != _run)
            {
                return;
            }

            state.PendingSteps.Clear();
            state.FreezeCheckpoint = false;
            _completed = true;
            if (task.State != TaskState.Terminated)
            {
                task.MarkCompleted();
                task.AppendInfo("MAF 流程走完。");
            }
        }

        task.NotifyChanged();
    }

    /// <summary>推进异常：记错误并让任务停在阻塞。</summary>
    private void Fail(int run, Exception exception)
    {
        lock (task.Gate)
        {
            if (run != _run || Halted)
            {
                return;
            }

            _failed = true;
            task.AppendError($"MAF 流程推进异常：{exception.Message}");
        }

        task.NotifyChanged();
    }
}