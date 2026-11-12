using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Runtime.Tasks;
using ExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>流程运行期：只持有活跃控制实例，转发命令并在退出时收口。流程语义在控制实例内。</summary>
public sealed class FlowRuntime(
    ExecutionRuntime executions,
    IFlowControlFactory controls) : IFlowRuntime
{
    private readonly Lock _gate = new();
    private readonly Dictionary<WorkTask, IFlowControl> _controls = [];

    /// <summary>要求持有任务 Gate：启动该任务的推进循环。</summary>
    public void Start(WorkTask task)
    {
        IFlowControl control = controls.Create(task);
        control.Start();
        Launch(task, control);
    }

    /// <summary>要求持有任务 Gate：按调度种子恢复推进循环，任务保持暂停不自动推进。</summary>
    public void Restore(WorkTask task, IReadOnlyList<FlowStepPath> seed)
    {
        IFlowControl control = controls.Create(task);
        control.Restore(seed);
        Launch(task, control);
    }

    /// <summary>登记控制实例，把待处理目标接到任务上供存档捕获，循环结束后把控制移出索引。</summary>
    private void Launch(WorkTask task, IFlowControl control)
    {
        task.Control = control;
        task.SeedSource = control.CaptureSeed;

        // 注册在推进循环启动之后，宿主拿到的推进循环一定已就绪
        lock (_gate)
        {
            _controls[task] = control;
        }

        _ = Task.Run(async () =>
        {
            await control.Loop.ConfigureAwait(false);
            lock (_gate)
            {
                _controls.Remove(task);
            }
        });
    }

    /// <summary>要求持有任务 Gate：暂停任务的推进循环并中断在跑执行。</summary>
    public void Pause(WorkTask task) => Find(task)?.Pause();

    /// <summary>要求持有任务 Gate：停止一个已启动的执行。</summary>
    public ErrorOr<Success> StopExecution(WorkTask task, ExecutionId id)
    {
        return Find(task) is { } control
            ? control.StopExecution(id)
            : controls.StopExecution(task, id);
    }

    /// <summary>要求持有任务 Gate：恢复任务的推进循环，并把可恢复的阻塞排入恢复队列。</summary>
    public void ResumeTask(WorkTask task, bool continueRestores = true) =>
        Find(task)?.Resume(continueRestores);

    /// <summary>要求持有任务 Gate 之外的调用：给定节点的产出已变，推进它的下游与祖先容器。</summary>
    public void Advance(WorkTask task, IReadOnlyList<FlowStepPath> nodes) =>
        Find(task)?.Advance(nodes);

    /// <summary>要求持有任务 Gate：对阻塞的执行节点重新执行，返回实际发出的重新执行目标数。</summary>
    public int Rerun(WorkTask task) => Find(task)?.Rerun() ?? 0;

    /// <summary>终止任务：控制实例停止全部执行，推进循环随之终止。推进循环已收口时只终止任务状态。</summary>
    public ErrorOr<Success> Terminate(WorkTask task)
    {
        if (Find(task) is { } control)
        {
            return control.Terminate();
        }

        lock (task.Gate)
        {
            return task.Terminate();
        }
    }

    /// <summary>退出时中断全部任务与推进循环并等待收口。任务控制状态与节点状态保留，供持久化收尾。</summary>
    public async Task ShutdownAsync()
    {
        List<IFlowControl> active;
        lock (_gate)
        {
            active = [.. _controls.Values];
        }

        foreach (IFlowControl control in active)
        {
            control.InterruptForShutdown();
        }

        // 先收口派发器，排队与在跑的执行随之结束，推进循环才能等到在途归零
        await executions.ShutdownAsync().ConfigureAwait(false);

        foreach (IFlowControl control in active)
        {
            await control.Loop.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _controls.Clear();
        }
    }

    private IFlowControl? Find(WorkTask task)
    {
        lock (_gate)
        {
            return _controls.GetValueOrDefault(task);
        }
    }
}