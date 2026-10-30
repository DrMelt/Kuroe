using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Graph;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务推进的宿主：每次提交为任务建一个推进循环，暂停、恢复、返工与终止经信号或状态迁移落地。
/// 执行节点状态由任务对象承载，这里只管流程生命周期。</summary>
public sealed class FlowEngine(
    ExecutionDispatcher dispatcher,
    IExecutionTargetResolver targetResolver) : IFlowRunner
{
    private readonly Lock _gate = new();
    private readonly Dictionary<WorkTask, TaskDriver> _drivers = [];
    private readonly HashSet<TaskRegistry> _registries = [];

    /// <summary>要求持有任务 Gate：启动该任务的推进循环。</summary>
    public void Start(WorkTask task)
    {
        TaskDriver driver = new(task, dispatcher, targetResolver);
        driver.Start();
        Launch(task, driver);
    }

    /// <summary>要求持有任务 Gate：按调度种子恢复推进循环，任务保持暂停不自动推进。</summary>
    public void Restore(WorkTask task, IReadOnlyList<NodePath> seed)
    {
        TaskDriver driver = new(task, dispatcher, targetResolver);
        driver.Restore(seed);
        Launch(task, driver);
    }

    /// <summary>登记推进循环与任务域，把待处理目标接到任务上供检查点捕获，循环结束后把驱动移出索引。</summary>
    private void Launch(WorkTask task, TaskDriver driver)
    {
        task.SeedSource = driver.CaptureSeed;

        // 注册在推进循环启动之后，宿主拿到的推进循环一定已就绪
        lock (_gate)
        {
            _drivers[task] = driver;
            _registries.Add(task.Registry);
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

    /// <summary>要求持有任务 Gate：恢复任务的推进循环。</summary>
    public void ResumeTask(WorkTask task)
    {
        Find(task)?.ResumeTask();
    }

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    public void Advance(WorkTask task, IReadOnlyList<NodePath> nodes)
    {
        List<NodePath> targets;
        lock (task.Gate)
        {
            targets = [.. task.Runtime.Notify(nodes)];
            task.Touch();
        }

        Find(task)?.Advance(targets);
    }

    /// <summary>要求持有任务 Gate：对被阻塞的执行节点再开一轮返工，返回实际发出的重跑目标数。</summary>
    public int Rework(WorkTask task)
    {
        IReadOnlyList<TaskRuntime.ReworkTarget> targets;
        lock (task.Gate)
        {
            targets = task.Runtime.PlanRework();
            foreach (IGrouping<NodePath, TaskRuntime.ReworkTarget> group in targets.GroupBy(target => target.Blocked))
            {
                task.Runtime.Unblock(group.Key, [.. group.Select(target => target.Node)]);
            }

            foreach (TaskRuntime.ReworkTarget target in targets)
            {
                task.Runtime.Invalidate(target.Node);
            }

            if (targets.Count > 0)
            {
                task.Touch();
            }
        }

        if (targets.Count > 0 && Find(task) is { } driver)
        {
            driver.Rework(targets);
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

    /// <summary>退出时中断全部任务与推进循环并等待收口。任务控制状态保留，供持久化恢复。</summary>
    public async Task ShutdownAsync()
    {
        List<TaskDriver> drivers;
        TaskRegistry[] registries;
        lock (_gate)
        {
            drivers = [.. _drivers.Values];
            registries = [.. _registries];
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
            _registries.Clear();
        }

        foreach (TaskRegistry registry in registries)
        {
            registry.Forget();
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
