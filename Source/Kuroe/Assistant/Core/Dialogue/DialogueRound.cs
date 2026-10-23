using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Pending;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话任务的一轮问答：把输入交给停在等待的输入节点，等本轮执行收口并把回复交回。
/// 回答的交付由等待型节点的宿主功能承担，这里只管一轮的挂载、观察与收口。</summary>
public sealed class DialogueRound(TaskRegistry registry, PendingService pendings)
{
    /// <summary>回答待输入的节点并等本轮产出执行收口，只报成败。过程增量写给 observer。</summary>
    public async Task<ErrorOr<Success>> AskAsync(
        TaskId id,
        string? nodePath,
        string input,
        IProcessSink? observer)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        (NodePath? Output, int Baseline) = task.UnderLock(() =>
        {
            INodeTask access = task;
            NodePath? output = access.Graph.ExecutableNodes
                .Where(node => access.Graph.ShapeOf(node.Path) is { DispatchesExecutions: true })
                .Select(node => node.Path)
                .FirstOrDefault();

            return (output, task.Executions.Count);
        });
        int executionBaseline = Baseline;

        if (Output is not { } target)
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

        // 观察者先于回答挂上：本轮执行可能紧接回答就收口，晚挂会漏掉通知
        Action unsubscribeAttached = task.SubscribeExecutionAttached(execution =>
        {
            if (execution.Input.Node != target)
            {
                return;
            }

            if (task.UnderLock(() => task.Executions.TakeWhile(candidate => !ReferenceEquals(candidate, execution)).Count() < executionBaseline))
            {
                return;
            }

            task.UnderLock(() =>
            {
                round = execution;

                return true;
            });

            if (observer is { } sink)
            {
                execution.Observe(sink);
            }
        });
        Action unsubscribeSettled = task.SubscribeExecutionSettled(execution =>
        {
            if (execution.Input.Node != target)
            {
                return;
            }

            if (task.UnderLock(() => task.Executions.TakeWhile(candidate => !ReferenceEquals(candidate, execution)).Count() < executionBaseline))
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

                    // 本轮产出已定，等反馈复位完成、任务回到等待再返回，避免下一轮回答撞上复位窗口；
                    // 任务不再推进时不会再回到等待，本轮结果仍然成立
                    if (task.State != TaskState.Running || Answering(id))
                    {
                        return await outcome.Task;
                    }
                }
                else
                {
                    TaskState state = task.State;
                    bool started = task.UnderLock(() => round is not null);

                    // 本轮执行已挂上时结果由它的收口给出，未挂上才按任务状态判定收口
                    if (!started && state is (TaskState.Blocked or TaskState.Canceled or TaskState.Done))
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
    /// Done 可能是挂点评估前的瞬态，真收口由 DialogueHost 重建兜底，这里只拦截阻塞。</summary>
    private async Task<ErrorOr<Success>> WaitForAnsweringAsync(WorkTask task)
    {
        while (true)
        {
            if (Answering(task.Id))
            {
                return Result.Success;
            }

            TaskState state = task.State;
            if (state is TaskState.Blocked or TaskState.Canceled)
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
