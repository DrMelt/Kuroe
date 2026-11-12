using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks;
using ProcessObserver = Kuroe.Flow.Execution.ProcessObserver;
using LoopExecutionId = Kuroe.Flow.Execution.ExecutionId;

namespace Kuroe.Flow.Control;

/// <summary>宿主对用户任务域的动作端口。实现负责提交策略与任务生命周期，宿主只经此提交与操作任务。</summary>
public interface ITaskControl
{
    /// <summary>按流程名提交任务。observer 收到该任务全部执行的过程事件。</summary>
    ErrorOr<TaskSnapshot> Submit(
        StartInputs inputs,
        FlowName flowName,
        string? title,
        ProcessObserver? observer = null);

    /// <summary>暂停任务。</summary>
    ErrorOr<Success> Pause(TaskId id);

    /// <summary>恢复任务。</summary>
    ErrorOr<Success> Resume(TaskId id);

    /// <summary>改任务标题。</summary>
    ErrorOr<Success> Rename(TaskId id, string title);

    /// <summary>对阻塞的执行节点重新执行。</summary>
    ErrorOr<Success> Rerun(TaskId id);

    /// <summary>停止一个已启动的执行。</summary>
    ErrorOr<Success> StopExecution(LoopExecutionId id);

    /// <summary>丢掉已完成或已终止的任务。</summary>
    IReadOnlyList<TaskId> ClearSettled();

    /// <summary>终止任务。</summary>
    ErrorOr<Success> StopTask(TaskId id);

    /// <summary>等待当前待写状态落盘。</summary>
    Task FlushAsync();

    /// <summary>退出时中断全部任务与执行并等待收口。</summary>
    Task ShutdownAsync();
}