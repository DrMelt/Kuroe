using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Pending;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话任务的一轮问答：把输入交给停在等待的输入节点，等本轮执行收口并把回复交回。
/// 回答的交付由等待型节点的宿主功能承担，这里只管一轮的挂载、观察与收口。</summary>
public sealed class DialogueRound(TaskRegistry registry, PendingService pendings)
{
    /// <summary>回答待输入的节点并等本轮产出执行收口，只报成败。过程增量与过程条目经回调送出。</summary>
    public async Task<ErrorOr<Success>> AskAsync(
        TaskId id,
        string? nodePath,
        string input,
        Action<string>? onText = null,
        Action<JournalEntry>? onEntry = null)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        NodePath? output = task.UnderLock(() =>
        {
            INodeTask access = task;
            return access.Graph.ExecutableNodes
                .Where(node => access.Graph.ShapeOf(node.Path) is { DispatchesExecutions: true })
                .Select(node => node.Path)
                .FirstOrDefault();
        });

        if (output is not { } target)
        {
            return [PendingErrors.NoDialogueTarget()];
        }

        ErrorOr<Success> waited = await WaitForAnsweringAsync(task);
        if (waited.IsError)
        {
            return waited.ErrorsOrEmptyList;
        }

        var outcome = new TaskCompletionSource<ErrorOr<Success>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Execution? round = null;
        int boundary = 0;
        Action unsubscribeAttached = () => { };
        Action unsubscribeSettled = () => { };

        // 观察者先于回答挂上：本轮执行可能紧接回答就收口，晚挂会漏掉通知。
        // 边界与订阅在同一任务锁内建立，旧执行的通知一律忽略。
        task.UnderLock(() =>
        {
            boundary = task.Executions.Count == 0
                ? 0
                : task.Executions.Max(execution => execution.Id.Value);

            unsubscribeAttached = task.SubscribeExecutionAttached(execution =>
            {
                if (execution.Id.Value <= boundary || execution.Input.Node != target)
                {
                    return;
                }

                bool accepted = task.UnderLock(() =>
                {
                    if (round is not null)
                    {
                        return false;
                    }

                    round = execution;
                    return true;
                });

                if (!accepted)
                {
                    return;
                }

                if (onText is { } textObserver)
                {
                    execution.TextEmitted += textObserver;
                }

                if (onEntry is { } entryObserver)
                {
                    execution.EntryEmitted += entryObserver;
                }
            });

            unsubscribeSettled = task.SubscribeExecutionSettled(execution =>
            {
                if (execution.Id.Value <= boundary || execution.Input.Node != target)
                {
                    return;
                }

                if (!task.UnderLock(() => ReferenceEquals(round, execution)))
                {
                    return;
                }

                if (execution.State == ExecutionState.Succeeded)
                {
                    outcome.TrySetResult(Result.Success);
                    return;
                }

                outcome.TrySetResult(PendingErrors.RoundFailed([.. execution.Snapshot().Failures]));
            });

            return true;
        });
        try
        {
            ErrorOr<Success> answered = pendings.Answer(id, nodePath, input);
            if (answered.IsError)
            {
                return answered.ErrorsOrEmptyList;
            }

            while (true)
            {
                if (outcome.Task.IsCompleted)
                {
                    if (outcome.Task.Result.IsError)
                    {
                        return await outcome.Task;
                    }

                    // 本轮产出已定，等反馈复位完成、任务回到等待再返回，避免下一轮回答撞上复位窗口。
                    // Done 可能是挂点评估前的瞬态，只有 Completed 才表示任务真正结束。
                    if (Answering(id) || task.Completed)
                    {
                        return await outcome.Task;
                    }

                    if (task.State is TaskState.Blocked or TaskState.Terminated or TaskState.Paused)
                    {
                        return [PendingErrors.DialogueSettled(task.State)];
                    }
                }
                else
                {
                    TaskState state = task.State;
                    bool started = task.UnderLock(() => round is not null);

                    // 本轮执行已挂上时结果由它的收口给出，未挂上才按任务状态判定收口。
                    if (!started && (task.Completed || state is TaskState.Blocked or TaskState.Terminated or TaskState.Paused))
                    {
                        return [PendingErrors.DialogueSettled(state)];
                    }
                }

                await Task.Delay(20);
            }
        }
        finally
        {
            unsubscribeAttached();
            unsubscribeSettled();
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
