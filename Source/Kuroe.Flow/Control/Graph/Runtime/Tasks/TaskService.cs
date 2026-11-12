using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>用户任务域对任务的动作：按流程名提交、重新执行、终止、改名。用户任务的提交策略在这里，域内生命周期由 <see cref="TaskActions"/> 承担。</summary>
/// <remarks>装配用户任务提交所需的动作与流程目录。</remarks>
public sealed class TaskService(
    TaskActions actions,
    IFlowRuntimeCatalog flows) : ITaskControl
{
    private readonly TaskActions _actions = actions;
    private readonly IFlowRuntimeCatalog _flows = flows;

    /// <summary>提交任务：按流程建任务、取宿主开始输入值并启动执行。observer 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> Submit(
        StartInputs inputs,
        FlowName flowName,
        string? title,
        ProcessObserver? observer = null)
    {
        ErrorOr<CompiledFlow> flow = _flows.FindCompiled(flowName);
        if (flow.IsError)
        {
            return flow.ErrorsOrEmptyList;
        }

        return _actions.SubmitCompiled(inputs, flow.Value, title, observer);
    }

    /// <summary>暂停任务：停止新的推进与派发，在跑的执行就地中断。</summary>
    public ErrorOr<Success> Pause(TaskId id) => _actions.Pause(id);

    /// <summary>恢复任务：暂停时恢复控制状态，存在恢复计划时派发恢复执行。</summary>
    public ErrorOr<Success> Resume(TaskId id) => _actions.Resume(id);

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title) => _actions.Rename(id, title);

    /// <summary>对阻塞的执行节点重新执行。</summary>
    public ErrorOr<Success> Rerun(TaskId id) => _actions.Rerun(id);

    /// <summary>停止一个已启动的执行。它所在的执行节点随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id) => _actions.StopExecution(id);

    /// <summary>丢掉已完成或已终止的任务，同时删除它们的存档。</summary>
    public IReadOnlyList<TaskId> ClearSettled() => _actions.ClearSettled();

    /// <summary>终止任务：在跑的执行随之中断，未走完的执行节点不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id) => _actions.StopTask(id);

    /// <summary>等待当前待写状态落盘。</summary>
    public Task FlushAsync() => _actions.FlushAsync();

    /// <summary>退出时中断全部任务与执行并等待收口。</summary>
    public Task ShutdownAsync() => _actions.ShutdownAsync();
}
