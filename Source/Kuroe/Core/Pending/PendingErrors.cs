using ErrorOr;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Pending;

/// <summary>等待型节点的错误构造。</summary>
public static class PendingErrors
{
    /// <summary>对话任务里没有可回话的执行节点，一问一答无从收口。</summary>
    public static Error NoDialogueTarget() => Error.Validation("Task.Ask", "任务里没有可回话的执行节点。");

    /// <summary>本轮判定出来前任务先行收口或阻塞，对话无法继续。</summary>
    public static Error Settled(TaskId task, TaskState state) =>
        Error.Conflict("Task.Ask", $"{task} {state.Label()}，无法继续对话。");

    /// <summary>本轮对话的执行没有成功收口，失败原因随错误带回。</summary>
    public static Error RoundFailed(IReadOnlyList<string> failures) => Error.Failure("Task.Ask",
        failures.Count > 0 ? string.Join("；", failures) : "本轮对话没有收口。");

    /// <summary>回答内容为空，不构成输入。</summary>
    public static Error EmptyAnswer() => Error.Validation("Task.Answer", "回答内容不能为空。");

    /// <summary>该任务没有停在给定等待种类的节点。</summary>
    public static Error NoAwaiting(TaskId task, PendingKind kind) => kind == PendingKind.Approve
        ? Error.Validation("Task.Approve", $"{task} 没有等待批准的审批节点。")
        : Error.Validation("Task.Answer", $"{task} 没有等待回答的输入节点。");

    /// <summary>任务已取消，外部输入无处交付。</summary>
    public static Error Canceled(TaskId task, PendingKind kind) => kind == PendingKind.Approve
        ? Error.Conflict("Task.Approve", $"{task} 已取消，无法批准。")
        : Error.Conflict("Task.Answer", $"{task} 已取消，无法回答。");

    /// <summary>多个节点等待回答，必须指定节点路径。</summary>
    public static Error AmbiguousInput(TaskId task, int count) => Error.Validation(
        "Task.Answer", $"{task} 有 {count} 个输入节点等待回答，请指定节点路径。");

    /// <summary>该节点没有等待回答的输入。</summary>
    public static Error NoAwaitingNode(string nodePath) => Error.Validation(
        "Task.Answer", $"节点「{nodePath}」没有等待回答的输入。");
}
