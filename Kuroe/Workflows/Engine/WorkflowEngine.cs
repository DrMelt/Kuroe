using Kuroe.Agent.Runs;
using Kuroe.Agent.Turns;
using Kuroe.Configuration;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Workflows.Tasks;
using Microsoft.Agents.AI.Workflows;

namespace Kuroe.Workflows.Engine;

/// <summary>任务推进的宿主：提交时组装框架 Workflow 并驱动其流式运行，
/// 批准、返工与取消经信号恢复或终止。单元与 agent 的状态仍由任务对象承载，这里只管流程生命周期。</summary>
sealed class WorkflowEngine(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    NodeModelResolver models,
    SettingsProvider settings)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, (StreamingRun Run, Task Driver)> _runs = [];
    private readonly Dictionary<TaskId, TaskCompletionSource> _recovery = [];
    private readonly HashSet<TaskId> _cancelling = [];
    private bool _stopping;

    /// <summary>要求持有任务 Gate：建首个单元并启动该任务的流程运行。</summary>
    public void Start(AgentTask task)
    {
        lock (task.Gate)
        {
            if (task.Units.Count == 0)
            {
                task.AddUnit(null, null, 0);
            }
        }

        StreamingRun run = FlowWorkflowFactory.Start(task, registry, dispatcher, models, settings);
        lock (_gate)
        {
            _runs[task.Id] = (run, Task.Run(() => DriveAsync(task.Id)));
        }
    }

    /// <summary>要求持有任务 Gate：批准等待放行的单元：同步把它们推进到下一步，再向流程运行发出继续信号，返回被批准的单元数。</summary>
    public int Approve(AgentTask task)
    {
        int waiting;
        lock (task.Gate)
        {
            List<WorkUnit> waitingUnits = [.. task.Units.Where(unit => unit.State == UnitState.AwaitingApproval)];
            waiting = waitingUnits.Count;
            if (waiting > 0)
            {
                foreach (WorkUnit unit in waitingUnits)
                {
                    MoveForward(task, unit);
                }

                task.Touch();
            }
        }

        if (waiting > 0)
        {
            Signal(task.Id, new FlowMessage { Intent = FlowIntent.Approved });
        }

        return waiting;
    }

    /// <summary>要求持有任务 Gate：单元从当前叶继续，需要展开时建子单元。</summary>
    private static void MoveForward(AgentTask task, WorkUnit unit)
    {
        int next = unit.NodeCursor + 1;
        FlowLeafExecutor.AdvanceUnit(task, unit, next);
    }

    /// <summary>要求持有任务 Gate：对被阻塞的单元再开一轮实施，itemIndex 为空时处理全部，返回被处理的单元数。</summary>
    public int Rework(AgentTask task, int? itemIndex)
    {
        int blocked;
        lock (task.Gate)
        {
            blocked = task.Units.Count(unit =>
                unit.State == UnitState.Blocked && (itemIndex is null || unit.ItemIndex == itemIndex));
            if (blocked > 0)
            {
                task.Touch();
            }
        }

        if (blocked > 0)
        {
            Signal(task.Id, new FlowMessage { Intent = FlowIntent.Reworked, ItemIndex = itemIndex });
        }

        return blocked;
    }

    /// <summary>取消任务：任务对象停止全部 agent，流程运行终止。</summary>
    public void Cancel(AgentTask task)
    {
        lock (task.Gate)
        {
            task.Cancel();
        }

        _ = StopAsync(task.Id);
    }

    /// <summary>退出时取消全部任务与流程运行并等待收口。</summary>
    public async Task ShutdownAsync()
    {
        List<(TaskId Id, StreamingRun Run, Task Driver)> live;
        lock (_gate)
        {
            _stopping = true;
            live = [.. _runs.Select(entry => (entry.Key, entry.Value.Run, entry.Value.Driver))];
        }

        // 先停运行再唤醒宿主循环，避免宿主清理与取消并发触碰框架已释放的资源
        foreach ((TaskId taskId, StreamingRun _, Task _) in live)
        {
            await CancelOnceAsync(taskId).ConfigureAwait(false);
        }

        foreach ((TaskId taskId, StreamingRun _, Task _) in live)
        {
            Awaken(taskId);
        }

        foreach ((TaskId _, StreamingRun _, Task driver) in live)
        {
            await driver.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _runs.Clear();
            _recovery.Clear();
        }

        await dispatcher.ShutdownAsync();
        registry.Forget();
    }

    /// <summary>唤醒该任务宿主循环的暂停点。没有挂起或已结束时不动作。</summary>
    private void Awaken(TaskId id)
    {
        lock (_gate)
        {
            if (_recovery.Remove(id, out TaskCompletionSource? gate))
            {
                gate.TrySetResult();
            }
        }
    }

    /// <summary>向该任务的流程运行投递意图消息并唤醒宿主循环。</summary>
    private void Signal(TaskId id, FlowMessage message)
    {
        (StreamingRun Run, Task Driver)? entry;
        TaskCompletionSource? gate;
        lock (_gate)
        {
            if (!_runs.TryGetValue(id, out var found))
            {
                registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{id} 的信号丢失：流程运行已结束。"));
                return;
            }

            entry = found;
            gate = _recovery.GetValueOrDefault(id);
        }

        // run 尚未收口时消息入队待下个 superstep 处理
        entry.Value.Run.TrySendMessageAsync(message).AsTask().GetAwaiter().GetResult();
        gate?.TrySetResult();
    }

    /// <summary>取消该任务的流程运行。停止后不再有新事件，宿主循环随之退出。</summary>
    private async Task StopAsync(TaskId id)
    {
        (StreamingRun Run, Task Driver)? entry;
        lock (_gate)
        {
            if (!_runs.TryGetValue(id, out var found))
            {
                return;
            }

            entry = found;
        }

        Awaken(id);
        await CancelOnceAsync(id).ConfigureAwait(false);
        await entry.Value.Driver.ConfigureAwait(false);
        lock (_gate)
        {
            _runs.Remove(id);
            _recovery.Remove(id);
        }
    }

    /// <summary>每个流程运行只取消一次，取消与退出的并发线程不会重复进入框架的取消路径。</summary>
    private async Task CancelOnceAsync(TaskId id)
    {
        StreamingRun? run;
        lock (_gate)
        {
            if (!_cancelling.Add(id) || !_runs.TryGetValue(id, out var entry))
            {
                return;
            }

            run = entry.Run;
        }

        await CancelQuietlyAsync(run);
    }

    /// <summary>运行可能已被宿主循环的收尾路径取消或释放，取消竞争视为已处理。</summary>
    private static async Task CancelQuietlyAsync(StreamingRun run)
    {
        try
        {
            await run.CancelRunAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>消费流程事件流。事件流在 RequestHalt 处结束：任务已完成则结束，否则等待宿主信号后继续。</summary>
    private async Task DriveAsync(TaskId id)
    {
        (StreamingRun Run, Task Driver) entry;
        lock (_gate)
        {
            if (!_runs.TryGetValue(id, out entry))
            {
                return;
            }
        }

        StreamingRun run = entry.Run;
        try
        {
            await DriveLoopAsync(id, run).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            registry.Report(new ExecutionNotice(NoticeLevel.Warning, $"{id} 的流程运行异常：{ex.Message}"));
        }

        lock (_gate)
        {
            _runs.Remove(id);
            _recovery.Remove(id);
        }

        try
        {
            await run.DisposeAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <summary>消费流程事件流。事件流在 RequestHalt 处结束：任务已完成则结束，否则等待宿主信号后继续。
    /// 恢复后的消费用阻塞模式，让新投递的宿主意图进入执行器后再回到暂停点判定。</summary>
    private async Task DriveLoopAsync(TaskId id, StreamingRun run)
    {
        while (true)
        {
            await foreach (WorkflowEvent _ in run.WatchStreamAsync(blockOnPendingRequest: false))
            {
            }

            RunStatus status = await run.GetStatusAsync().ConfigureAwait(false);
            if (status is RunStatus.Ended or RunStatus.NotStarted || _stopping)
            {
                break;
            }

            if (IsDone(id))
            {
                break;
            }

            // RequestHalt 暂停点：等待宿主批准或返工信号
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _recovery[id] = gate;
            }

            await gate.Task.ConfigureAwait(false);
            _recovery.Remove(id);
        }
    }

    /// <summary>任务是否已走完。任务已被清理时视为结束。</summary>
    private bool IsDone(TaskId id)
    {
        if (registry.Find(id) is { IsError: false } found)
        {
            lock (found.Value.Gate)
            {
                return found.Value.State == TaskState.Done;
            }
        }

        return true;
    }
}