using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Graph;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务推进的宿主：每次提交为任务建一个推进循环，暂停、恢复、重新执行与终止经信号或状态迁移落地。
/// 执行节点状态由任务对象承载，这里只管流程生命周期。</summary>
public sealed class FlowEngine(
    ExecutionDispatcher dispatcher,
    IExecutionRunnerSource runners,
    IExecutionTargetResolver targetResolver) : IFlowRunner
{
    private readonly Lock _gate = new();
    private readonly Dictionary<WorkTask, TaskDriver> _drivers = [];

    /// <summary>要求持有任务 Gate：启动该任务的推进循环。</summary>
    public void Start(WorkTask task)
    {
        task.Registry.RestorerSource = runners as IExecutionRestorerSource;
        TaskDriver driver = new(task, dispatcher, runners, runners as IExecutionRestorerSource, targetResolver);
        driver.Start();
        Launch(task, driver);
    }

    /// <summary>要求持有任务 Gate：按调度种子恢复推进循环，任务保持暂停不自动推进。</summary>
    public void Restore(WorkTask task, IReadOnlyList<NodePath> seed)
    {
        task.Registry.RestorerSource = runners as IExecutionRestorerSource;
        TaskDriver driver = new(task, dispatcher, runners, runners as IExecutionRestorerSource, targetResolver);
        driver.Restore(seed);
        Launch(task, driver);
    }

    /// <summary>登记推进循环，把待处理目标接到任务上供存档捕获，循环结束后把驱动移出索引。</summary>
    private void Launch(WorkTask task, TaskDriver driver)
    {
        task.SeedSource = driver.CaptureSeed;

        // 注册在推进循环启动之后，宿主拿到的推进循环一定已就绪
        lock (_gate)
        {
            _drivers[task] = driver;
        }

        _ = Task.Run(async () =>
        {
            await driver.Loop.ConfigureAwait(false);
            lock (_gate)
            {
                _drivers.Remove(task);
            }
        });
    }

    /// <summary>要求持有任务 Gate：暂停任务的推进循环并中断在跑执行。</summary>
    public void Pause(WorkTask task)
    {
        Find(task)?.Pause();
    }

    /// <summary>要求持有任务 Gate：停止一个已启动的执行。</summary>
    public ErrorOr<Success> StopExecution(WorkTask task, Execution execution)
    {
        if (Find(task) is not null)
        {
            return TaskDriver.StopExecution(execution);
        }

        if (!execution.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(execution.Id, "停止")];
        }

        if (execution.Snapshot().StartedAt is null)
        {
            return [ExecutionErrors.ExecutionNotStarted(execution.Id)];
        }

        return StopDirect(execution);
    }

    private static ErrorOr<Success> StopDirect(Execution execution)
    {
        execution.Interrupt("已停止执行。");

        return Result.Success;
    }

    /// <summary>要求持有任务 Gate：恢复任务的推进循环，并把可恢复的阻塞排入恢复队列。</summary>
    public void ResumeTask(WorkTask task, bool continueRestores = true)
    {
        if (Find(task) is not { } driver)
        {
            return;
        }

        IReadOnlyList<TaskRuntime.RestorePlan> restores = continueRestores
            ? task.Runtime.PlanRestores()
            : [];
        driver.ResumeTask(restores, clearPendingRestores: !continueRestores);
    }

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    public void Advance(WorkTask task, IReadOnlyList<NodePath> nodes)
    {
        List<NodePath> targets;
        lock (task.Gate)
        {
            targets = [.. task.Runtime.Notify(nodes)];
            task.Runtime.ClearRestoreStates(targets);
            if (targets.Count > 0)
            {
                task.Journal.AppendInfo("上游产出变化，清除下游恢复状态。");
            }

            task.Touch();
        }

        Find(task)?.Advance(targets);
        task.NotifyChanged();
    }

    /// <summary>要求持有任务 Gate：对阻塞的执行节点重新执行，返回实际发出的重新执行目标数。</summary>
    public int Rerun(WorkTask task)
    {
        IReadOnlyList<TaskRuntime.RerunTarget> targets;
        lock (task.Gate)
        {
            targets = task.Runtime.PlanReruns();
            foreach (IGrouping<NodePath, TaskRuntime.RerunTarget> group in targets.GroupBy(target => target.Blocked))
            {
                task.Runtime.ClearRestoreState(group.Key);
                task.Runtime.Unblock(group.Key, [.. group.Select(target => target.Node)]);
                task.Journal.AppendInfo($"重新执行节点 {group.Key}，清除恢复状态。");
            }

            foreach (TaskRuntime.RerunTarget target in targets)
            {
                task.Runtime.Invalidate(target.Node);
            }

            if (targets.Count > 0)
            {
                task.Touch();
            }
        }

        if (targets.Count > 0)
        {
            if (Find(task) is { } driver)
            {
                driver.Rerun(targets);
            }

            task.NotifyChanged();
        }

        return targets.Count;
    }

    /// <summary>终止任务：任务对象停止全部执行，推进循环随之终止。推进循环已收口时只终止任务状态。</summary>
    public ErrorOr<Success> Cancel(WorkTask task)
    {
        if (Find(task) is { } driver)
        {
            return driver.Stop();
        }

        lock (task.Gate)
        {
            return task.Terminate();
        }
    }

    /// <summary>退出时中断全部任务与推进循环并等待收口。任务控制状态与节点状态保留，供持久化收尾。</summary>
    public async Task ShutdownAsync()
    {
        List<TaskDriver> drivers;
        lock (_gate)
        {
            drivers = [.. _drivers.Values];
        }

        foreach (TaskDriver driver in drivers)
        {
            driver.InterruptForShutdown();
        }

        // 先收口派发器，排队与在跑的执行随之结束，推进循环才能等到在途归零
        await dispatcher.ShutdownAsync();

        foreach (TaskDriver driver in drivers)
        {
            await driver.Loop.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _drivers.Clear();
        }
    }

    private TaskDriver? Find(WorkTask task)
    {
        lock (_gate)
        {
            return _drivers.GetValueOrDefault(task);
        }
    }
}
