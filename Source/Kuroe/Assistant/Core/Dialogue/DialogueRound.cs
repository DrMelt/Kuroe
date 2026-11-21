using ErrorOr;
using Kuroe.Assistant.Dialogue.Flow;
using Kuroe.Pending;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话任务的一轮问答：把输入交给停在等待的输入节点，等本轮执行收口并把回复交回。
/// 回答的交付由等待型节点的宿主功能承担，这里只管一轮的挂载、观察与收口。</summary>
internal sealed class DialogueRound(TaskRegistry registry, PendingService pendings)
{
    /// <summary>回答待输入的节点并等本轮产出执行收口，只报成败。过程事件经 <paramref name="observer"/> 送出。</summary>
    internal async Task<ErrorOr<Success>> AskAsync(
        TaskId id,
        string input,
        ProcessObserver? observer = null)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        string? output = task.UnderLock(() =>
            task.Snapshot().View.Steps
                .FirstOrDefault(step => step.Label == DialogueFlow.ReplyLabel)?.Path);

        if (output is not { } target)
        {
            return [PendingErrors.NoDialogueTarget()];
        }

        ErrorOr<Success> waited = await WaitForAnsweringAsync(task);
        if (waited.IsError)
        {
            return waited.ErrorsOrEmptyList;
        }

        // 观察者先于回答挂上：本轮执行可能紧接回答就收口，晚挂会漏掉通知。
        using DialogueRoundWatch watch = DialogueRoundWatch.Attach(task, target, observer);
        ErrorOr<Success> answered = pendings.Answer(id, null, input);
        if (answered.IsError)
        {
            return answered.ErrorsOrEmptyList;
        }

        return await WaitForOutcomeAsync(id, task, watch);
    }

    /// <summary>等本轮结果：失败直接返回，成功后等任务回到等待，收口或阻塞报对话无法继续。</summary>
    private async Task<ErrorOr<Success>> WaitForOutcomeAsync(
        TaskId id,
        WorkTask task,
        DialogueRoundWatch watch)
    {
        while (true)
        {
            if (watch.Outcome.IsCompleted)
            {
                if (watch.Outcome.Result.IsError)
                {
                    return await watch.Outcome;
                }

                // 本轮产出已定，等反馈复位完成、任务回到等待再返回，避免下一轮回答撞上复位窗口。
                // Done 可能是挂点评估前的瞬态，只有 Completed 才表示任务真正结束。
                if (Answering(id) || task.Completed)
                {
                    return await watch.Outcome;
                }

                if (task.State is TaskState.Blocked or TaskState.Terminated or TaskState.Paused)
                {
                    return [PendingErrors.DialogueSettled(task.State)];
                }
            }
            else
            {
                TaskState state = task.State;

                // 本轮执行已挂上时结果由它的收口给出，未挂上才按任务状态判定收口。
                if (!watch.Started && (task.Completed || state is TaskState.Blocked or TaskState.Terminated or TaskState.Paused))
                {
                    return [PendingErrors.DialogueSettled(state)];
                }
            }

            await Task.Delay(20);
        }
    }

    /// <summary>等任务停在一个待回答的节点上：提交后推进循环异步把挂点评估出来，过早回答命中不了输入节点。
    /// Done 可能是挂点评估前的瞬态，只有 Completed 才表示任务真正结束。</summary>
    private async Task<ErrorOr<Success>> WaitForAnsweringAsync(WorkTask task)
    {
        while (true)
        {
            if (Answering(task.Id))
            {
                return Result.Success;
            }

            TaskState state = task.State;
            if (task.Completed || state is TaskState.Blocked or TaskState.Terminated or TaskState.Paused)
            {
                return [PendingErrors.DialogueSettled(state)];
            }

            await Task.Delay(20);
        }
    }

    /// <summary>任务里是否有停在等待回答的节点。</summary>
    private bool Answering(TaskId id) =>
        pendings.Pending(id) is { IsError: false } pending
        && pending.Value.Any(node => node.Kind == PendingKind.Answer);
}