using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>任务域的错误构造。</summary>
static class TaskErrors
{
    public static Error NotFound(int value) => Error.NotFound(FlowErrorCodes.TaskNotFound, $"没有任务 #{value}。");

    /// <summary>提交任务时目标为空。</summary>
    public static Error EmptyGoal() => Error.Validation("Task.Goal", "目标不能为空。");

    /// <summary>该任务没有被阻塞的执行节点。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rework", $"{task} 没有被阻塞的执行节点。");

    /// <summary>端口产出的形状不合法，原因要能直接回给模型改正。</summary>
    public static Error Ports(string reason) => Error.Validation(
        "Task.Ports", $"{reason}。要提交端口名到文本的 JSON 对象。");
}
