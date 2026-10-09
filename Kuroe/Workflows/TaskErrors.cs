using ErrorOr;
using Kuroe.Shared;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Workflows;

namespace Kuroe.Workflows;

/// <summary>任务域的错误构造。</summary>
static class TaskErrors
{
    public static Error NotFound(int value) => Error.NotFound(ErrorCodes.TaskNotFound, $"没有任务 #{value}。");

    /// <summary>提交任务时目标为空。</summary>
    public static Error EmptyGoal() => Error.Validation("Task.Goal", "目标不能为空。");

    /// <summary>该任务没有等待批准的产出。</summary>
    public static Error NotAwaiting(TaskId task) =>
        Error.Validation("Task.Approve", $"{task} 没有等待批准的产出。");

    /// <summary>该任务没有等待回答的输入节点。</summary>
    public static Error NoAwaitingInput(TaskId task) =>
        Error.Validation("Task.Answer", $"{task} 没有等待回答的输入节点。");

    /// <summary>回答内容为空，不构成输入。</summary>
    public static Error EmptyAnswer() =>
        Error.Validation("Task.Answer", "回答内容不能为空。");

    /// <summary>多个输入节点等待回答，必须指定节点名。</summary>
    public static Error AmbiguousInput(TaskId task, int count) =>
        Error.Validation("Task.Answer", $"{task} 有 {count} 个输入节点等待回答，请指定节点名。");

    /// <summary>该节点没有等待回答的输入。</summary>
    public static Error NoAwaitingInputNode(string nodeName) =>
        Error.Validation("Task.Answer", $"节点「{nodeName}」没有等待回答的输入。");

    /// <summary>对话任务里没有可回话的执行节点，一问一答无从收口。</summary>
    public static Error NoDialogueTarget() =>
        Error.Validation("Task.Ask", "任务里没有可回话的执行节点。");

    /// <summary>等待回复期间任务先行收口，对话无法再继续。</summary>
    public static Error DialogueSettled(TaskId task, TaskState state) =>
        Error.Conflict("Task.Ask", $"{task} 已处于 {state}，对话提前结束。");

    /// <summary>该任务没有被阻塞的条目。</summary>
    public static Error NotBlocked(TaskId task) =>
        Error.Validation("Task.Rework", $"{task} 没有被阻塞的节点或条目。");

    /// <summary>流程里没有可启动的执行节点：引用依赖构成环且环内没有输入节点作启动点。</summary>
    public static Error NoRoot() => Error.Validation(
        "Task.NoRoot", "流程引用关系构成环且环内没有输入节点，找不到可启动的执行节点，请检查 From。");

    /// <summary>条目数组的形状不合法，原因要能直接回给模型改正。</summary>
    public static Error Items(string reason) => Error.Validation(
        "Task.Items", $"{reason}。要提交对象数组，每项含 Title、Instruction、Acceptance。");

    /// <summary>端口产出的形状不合法，原因要能直接回给模型改正。</summary>
    public static Error Ports(string reason) => Error.Validation(
        "Task.Ports", $"{reason}。要提交端口名到文本的 JSON 对象。");
}
