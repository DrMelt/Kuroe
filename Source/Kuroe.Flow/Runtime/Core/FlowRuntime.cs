using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Runtime;

/// <summary>流程运行期：只持有活跃控制实例，转发命令并在退出时收口。流程语义在控制实例内。</summary>
public sealed class FlowRuntime(
    IExecutionRuntime executions) : IFlowRuntime
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ITaskFlowContext, IFlowControl> _controls = [];

    /// <summary>要求持有任务 Gate：启动该任务的推进循环。</summary>
    public void Start(ITaskFlowHost task)
    {
        IFlowControl control = task.Controls.CreateControl(task);
        Attach(task, control);
        control.Start();
        Track(task, control);
    }

    /// <summary>要求持有任务 Gate：按恢复种子启动推进循环，任务保持暂停不自动推进。</summary>
    public void Restore(ITaskFlowHost task, FlowSeed seed)
    {
        IFlowControl control = task.Controls.CreateControl(task);
        Attach(task, control);
        control.Restore(seed);
        Track(task, control);
    }

    /// <summary>接上控制实例并登记活跃索引，启动期间任务能读到自身控制。</summary>
    private void Attach(ITaskFlowHost task, IFlowControl control)
    {
        task.AttachControl(control);

        lock (_gate)
        {
            _controls[task] = control;
        }
    }

    /// <summary>控制实例完成后把控制移出索引。</summary>
    private void Track(ITaskFlowHost task, IFlowControl control)
    {
        _ = Task.Run(async () =>
        {
            await control.Completion.ConfigureAwait(false);
            lock (_gate)
            {
                if (_controls.TryGetValue(task, out IFlowControl? current)
                    && ReferenceEquals(current, control))
                {
                    _controls.Remove(task);
                }
            }
        });
    }

    /// <summary>要求持有任务 Gate：暂停任务的推进循环并中断在跑执行。</summary>
    public void Pause(ITaskFlowContext task) => Find(task)?.Pause();

    /// <summary>按执行号取出本任务的一个已启动执行并停止它。</summary>
    public ErrorOr<Success> StopExecution(ITaskFlowContext task, ExecutionId id)
    {
        IExecution? execution = task.UnderLock(() =>
            task.Executions.FirstOrDefault(candidate => candidate.Id == id));
        if (execution is null)
        {
            return [ExecutionErrors.ExecutionNotFound(id.Value)];
        }

        if (!execution.IsLive)
        {
            return [ExecutionErrors.ExecutionSettled(execution.Id, "停止")];
        }

        if (execution.Snapshot().StartedAt is null)
        {
            return [ExecutionErrors.ExecutionNotStarted(execution.Id)];
        }

        executions.Interrupt(execution, "已停止执行。");

        return Result.Success;
    }

    /// <summary>要求持有任务 Gate：恢复任务的推进循环，并把可恢复的阻塞排入恢复队列。</summary>
    public void ResumeTask(ITaskFlowContext task, bool continueRestores = true) =>
        Find(task)?.Resume(continueRestores);

    /// <summary>要求持有任务 Gate 之外的调用：给定步骤的产出已变，推进它的下游与祖先容器。</summary>
    public void Advance(ITaskFlowContext task, IReadOnlyList<FlowStepPath> steps) =>
        Find(task)?.Advance(steps);

    /// <summary>要求持有任务 Gate：对阻塞的步骤重新执行，返回实际发出的重新执行目标数。</summary>
    public int Rerun(ITaskFlowContext task) => Find(task)?.Rerun() ?? 0;

    /// <summary>终止任务：控制实例停止全部执行，推进循环随之终止。推进循环已收口时只终止任务状态。</summary>
    public ErrorOr<Success> Terminate(ITaskFlowContext task)
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

    /// <summary>退出时中断全部任务与推进循环并等待收口。任务控制状态与步骤状态保留，供持久化收尾。</summary>
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
            await control.Completion.ConfigureAwait(false);
        }

        lock (_gate)
        {
            _controls.Clear();
        }
    }

    private IFlowControl? Find(ITaskFlowContext task)
    {
        lock (_gate)
        {
            return _controls.GetValueOrDefault(task);
        }
    }

    /// <summary>活跃控制实例数。</summary>
    public int ActiveControls
    {
        get
        {
            lock (_gate)
            {
                return _controls.Count;
            }
        }
    }
}