using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Agent;

namespace Kuroe.Workflows;

/// <summary>任务域的错误构造。</summary>
static class TaskErrors
{
    /// <summary>该任务已有一轮前台对话在跑。</summary>
    public static Error Busy(TaskId task) =>
        Error.Conflict("Task.Busy", $"{task} 有一轮对话正在进行，切到别的任务或等它结束。");

    public static Error NotFound(int value) => Error.NotFound(ErrorCodes.TaskNotFound, $"没有任务 #{value}。");

    /// <summary>提交任务时目标为空。</summary>
    public static Error EmptyGoal() => Error.Validation("Task.Goal", "目标不能为空。");

    /// <summary>该任务没有等待批准的步骤。</summary>
    public static Error NotAwaiting(TaskId task) =>
        Error.Validation("Task.Approve", $"{task} 没有等待批准的步骤。");

    /// <summary>该任务没有被阻塞的条目。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rework", $"{task} 没有被阻塞的步骤或条目。");

    /// <summary>条目数组的形状不合法，原因要能直接回给模型改正。</summary>
    public static Error Items(string reason) => Error.Validation(
        "Task.Items", $"{reason}。要提交对象数组，每项含 Title、Instruction、Acceptance。");
}
