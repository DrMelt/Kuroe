using ErrorOr;
using Kuroe.Flow.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务域的错误构造。</summary>
internal static class TaskErrors
{
    /// <summary>任务号不存在。</summary>
    public static Error NotFound(string reference) =>
        Error.NotFound(FlowErrorCodes.TaskNotFound, $"没有任务 {reference}。");

    /// <summary>一个引用匹配到多个任务。</summary>
    public static Error Ambiguous(string reference) =>
        Error.Validation(FlowErrorCodes.TaskNotFound, $"任务引用 {reference} 匹配多个任务，请给出完整键。");

    /// <summary>该任务没有被阻塞的步骤。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rerun", $"{task} 没有被阻塞的步骤。");

    /// <summary>任务已暂停，不能推进或交付输入。</summary>
    public static Error Paused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskPaused, $"{task} 已暂停。");

    /// <summary>任务已是暂停状态。</summary>
    public static Error AlreadyPaused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskAlreadyPaused, $"{task} 已暂停。");

    /// <summary>任务不在暂停状态，也没有可恢复的阻塞。</summary>
    public static Error NotPaused(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskNotPaused, $"{task} 未暂停，也没有可恢复的执行。");

    /// <summary>任务已终止。</summary>
    public static Error Terminated(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskTerminated, $"{task} 已终止。");

    /// <summary>已完成的任务不能暂停。</summary>
    public static Error DoneCannotPause(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskDoneCannotPause, $"{task} 已完成，不能暂停。");

    /// <summary>已完成的任务不能终止。</summary>
    public static Error DoneCannotTerminate(TaskId task) =>
        Error.Conflict(FlowErrorCodes.TaskDoneCannotTerminate, $"{task} 已完成，不能终止。");
}