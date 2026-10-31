using ErrorOr;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>用户任务域对任务的动作：按流程名提交、返工、取消、改名。用户任务的提交策略在这里，域内生命周期由 <see cref="TaskActions"/> 承担。</summary>
/// <remarks>装配用户任务提交所需的动作与流程目录。</remarks>
public sealed class TaskService(
    TaskActions actions,
    IFlowRuntimeCatalog flows)
{
    private readonly TaskActions _actions = actions;
    private readonly IFlowRuntimeCatalog _flows = flows;

    /// <summary>提交任务：按流程建任务、取宿主开始输入值并启动执行。onContent 与 onEntry 收到该任务全部执行的过程事件。</summary>
    public ErrorOr<TaskSnapshot> Submit(
        StartInputs inputs,
        FlowName flowName,
        string? title,
        Action<ProcessContent>? onContent = null,
        Action<JournalEntry>? onEntry = null)
    {
        ErrorOr<CompiledFlow> flow = _flows.FindCompiled(flowName);
        if (flow.IsError)
        {
            return flow.ErrorsOrEmptyList;
        }

        return _actions.SubmitCompiled(inputs, flow.Value, title, onContent, onEntry);
    }

    /// <summary>暂停任务：停止新的推进与派发，在跑的执行就地中断。</summary>
    public ErrorOr<Success> Pause(TaskId id) => _actions.Pause(id);

    /// <summary>恢复暂停的任务。</summary>
    public ErrorOr<Success> Resume(TaskId id) => _actions.Resume(id);

    /// <summary>改任务标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title) => _actions.Rename(id, title);

    /// <summary>对被阻塞的执行节点再开一轮实施。</summary>
    public ErrorOr<Success> Rework(TaskId id) => _actions.Rework(id);

    /// <summary>取消一个执行。它所在的执行节点随后被阻塞，任务不再自动推进。</summary>
    public ErrorOr<Success> StopExecution(ExecutionId id) => _actions.StopExecution(id);

    /// <summary>丢掉已完成或已终止的任务，同时删除它们的检查点。</summary>
    public IReadOnlyList<TaskId> ClearSettled() => _actions.ClearSettled();

    /// <summary>终止任务：在跑的执行全部取消，未走完的执行节点不再推进。</summary>
    public ErrorOr<Success> StopTask(TaskId id) => _actions.StopTask(id);

    /// <summary>退出时中断全部任务与执行并等待收口。</summary>
    public Task ShutdownAsync() => _actions.ShutdownAsync();
}
