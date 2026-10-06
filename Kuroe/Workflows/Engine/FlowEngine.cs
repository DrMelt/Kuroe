using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Workflows.Engine;

/// <summary>任务推进的宿主：每次提交为任务建一个推进循环，批准、返工与取消经信号恢复或终止。
/// 执行节点状态由任务对象承载，这里只管流程生命周期。</summary>
sealed class FlowEngine(
    TaskRegistry registry,
    RunDispatcher dispatcher,
    NodeModelResolver models) : IFlowRunner
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, TaskDriver> _runs = [];

    /// <summary>要求持有任务 Gate：启动该任务的推进循环。</summary>
    public void Start(WorkTask task)
    {
        TaskDriver driver = new(task, registry, dispatcher, models);
        driver.Start();

        // 注册在推进循环启动之后，宿主拿到的推进循环一定已就绪
        lock (_gate)
        {
            _runs[task.Id] = driver;
        }

        // 推进循环结束后把驱动移出索引，任务仍留在注册表里供查询
        _ = Task.Run(async () =>
        {
            await driver.Loop.ConfigureAwait(false);
            lock (_gate)
            {
                _runs.Remove(task.Id);
            }
        });
    }

    /// <summary>要求持有任务 Gate：批准待批准的产出，空列表时放行全部待批准节点与容器，再发出继续信号，返回被批准的数量。</summary>
    public int Approve(WorkTask task, IReadOnlyList<RunId> runs)
    {
        int approved;
        lock (task.Gate)
        {
            approved = runs.Count == 0
                ? task.Runtime.AwaitingRuns.Count + task.Runtime.AwaitingContainers.Count
                : task.Runtime.CountAwaitingRuns(runs);
            if (approved > 0)
            {
                task.Touch();
            }
        }

        if (approved > 0 && Find(task.Id) is { } driver)
        {
            driver.Approve(runs);
        }

        return approved;
    }

    /// <summary>要求持有任务 Gate：回答等待输入的节点，回答即产出并放行下游，然后发出继续信号。</summary>
    public ErrorOr<Success> Answer(WorkTask task, string? nodeName, string input)
    {
        ErrorOr<List<int>> result;
        lock (task.Gate)
        {
            result = task.Runtime.Answer(nodeName, input);
            if (!result.IsError)
            {
                task.Touch();
            }
        }

        // 推进循环只在任务收口后移除，回答成功时任务必未收口，推进循环一定还在
        if (!result.IsError)
        {
            Find(task.Id)?.Answer(result.Value);
        }

        return result.IsError ? result.ErrorsOrEmptyList : Result.Success;
    }

    /// <summary>要求持有任务 Gate：对被阻塞的节点再开一轮返工，itemIndex 为空时处理全部，返回实际发出的重跑目标数。</summary>
    public int Rework(WorkTask task, int? itemIndex)
    {
        IReadOnlyList<(int Blocked, int Node, int? Item)> targets;
        lock (task.Gate)
        {
            targets = task.Runtime.PlanRework(itemIndex);
            foreach (IGrouping<int, (int Blocked, int Node, int? Item)> group in targets.GroupBy(pair => pair.Blocked))
            {
                task.Runtime.Unblock(group.Key, [.. group.Select(pair => (pair.Node, pair.Item))]);
            }

            foreach ((int _, int node, int? item) in targets)
            {
                task.Runtime.Invalidate(node, item);
            }

            if (targets.Count > 0)
            {
                task.Touch();
            }
        }

        if (targets.Count > 0 && Find(task.Id) is { } driver)
        {
            driver.Rework(targets);
        }

        return targets.Count;
    }

    /// <summary>取消任务：任务对象停止全部 run，推进循环随之终止。推进循环已收口时只停任务。</summary>
    public void Cancel(WorkTask task)
    {
        if (Find(task.Id) is { } driver)
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
        lock (_gate)
        {
            drivers = [.. _runs.Values];
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
            _runs.Clear();
        }

        await dispatcher.ShutdownAsync();
        registry.Forget();
    }

    private TaskDriver? Find(TaskId id)
    {
        lock (_gate)
        {
            return _runs.GetValueOrDefault(id);
        }
    }
}
