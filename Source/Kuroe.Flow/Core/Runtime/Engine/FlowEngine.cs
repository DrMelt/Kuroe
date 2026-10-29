using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Graph;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>任务推进的宿主：每次提交为任务建一个推进循环，返工与取消经信号恢复或终止。
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

        // 注册在推进循环启动之后，宿主拿到的推进循环一定已就绪
        lock (_gate)
        {
            _drivers[task] = driver;
            _registries.Add(task.Registry);
        }

        // 推进循环结束后把驱动移出索引，任务仍留在注册表里供查询
        _ = Task.Run(async () =>
        {
            await driver.Loop.ConfigureAwait(false);
            lock (_gate)
            {
                _drivers.Remove(task);
            }
        });
    }

    /// <summary>给定节点的产出已变，推进它的下游与祖先容器。要求持有任务 Gate 之外的调用。</summary>
    public void Resume(WorkTask task, IReadOnlyList<NodePath> nodes)
    {
        List<NodePath> targets;
        lock (task.Gate)
        {
            targets = [.. task.Runtime.Notify(nodes)];
            task.Touch();
        }

        Find(task)?.Resume(targets);
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

    /// <summary>取消任务：任务对象停止全部执行，推进循环随之终止。推进循环已收口时只停任务。</summary>
    public void Cancel(WorkTask task)
    {
        if (Find(task) is { } driver)
        {
            driver.Stop();
            return;
        }

        lock (task.Gate)
        {
            task.Cancel();
        }
    }

    /// <summary>退出时取消全部任务与推进循环并等待收口。</summary>
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
            driver.Stop();
        }

        foreach (TaskDriver driver in drivers)
        {
            await driver.Loop.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _drivers.Clear();
            _registries.Clear();
        }

        await dispatcher.ShutdownAsync();

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
