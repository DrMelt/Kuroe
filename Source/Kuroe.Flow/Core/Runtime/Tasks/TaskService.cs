using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Flow.Execution.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>用户任务域对任务的动作：按流程名提交、暂停、恢复、重新执行、终止与改名。</summary>
public sealed class TaskService(TaskActions actions) : ITaskControl
{
    private readonly TaskActions _actions = actions;

    /// <summary>提交任务：按流程名取负责的框架建任务并启动执行。observer 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> Submit(
        StartInputs inputs,
        FlowName flowName,
        string? title,
        ProcessObserver? observer = null) =>
        _actions.Submit(flowName, inputs, title, observer);

    /// <summary>暂停任务：停止新的推进与派发，在跑的执行就地中断。</summary>
    public ErrorOr<Success> Pause(TaskId id) => _actions.Pause(id);

    /// <summary>恢复任务：暂停时恢复控制状态，存在恢复计划时派发恢复执行。</summary>
    public ErrorOr<Success> Resume(TaskId id) => _actions.Resume(id);

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title) => _actions.Rename(id, title);

    /// <summary>对阻塞的步骤重新执行。</summary>
    public ErrorOr<Success> Rerun(TaskId id) => _actions.Rerun(id);

    /// <summary>停止一个已启动的执行。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id) => _actions.StopExecution(id);

    /// <summary>丢掉已完成或已终止的任务，同时删除它们的存档。</summary>
    public IReadOnlyList<TaskId> ClearSettled() => _actions.ClearSettled();

    /// <summary>终止任务：在跑的执行随之中断，未走完的步骤不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id) => _actions.StopTask(id);

    /// <summary>等待当前待写状态落盘。</summary>
    public Task FlushAsync() => _actions.FlushAsync();

    /// <summary>退出时中断全部任务与执行并等待收口。</summary>
    public Task ShutdownAsync() => _actions.ShutdownAsync();
}