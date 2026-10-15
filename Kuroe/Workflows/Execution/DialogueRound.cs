using ErrorOr;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Execution.Tasks;
using Kuroe.Flows.Shared.Execution;
using Kuroe.Flows.Shared.Execution.Runs;
using Kuroe.Flows.Shared.Execution.Turns;
using Kuroe.Flows.Shared.RuntimeGraph;
using Kuroe.Flows.Shared.Waiting;
using Kuroe.Waiting;

namespace Kuroe.Workflows.Execution;

/// <summary>对话任务的一轮问答：把输入交给停在等待的输入节点，等本轮 run 收口并把回复交回。
/// 回答的交付由等待型节点的宿主功能承担，这里只管一轮的挂载、观察与收口。</summary>
public sealed class DialogueRound(TaskRegistry registry, WaitService waits)
{
    /// <summary>回答待输入的节点并等待本轮产出 run 收口，返回本轮回复文本。过程增量写给 observer。</summary>
    public async Task<ErrorOr<string>> AskAsync(
        TaskId id,
        string? nodeName,
        string input,
        ITurnSink? observer,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [WaitErrors.EmptyAnswer()];
        }

        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        (int? Output, int Baseline) = task.UnderLock(() =>
        {
            INodeTask access = task;
            int? output = access.Graph.ExecutableNodes
                .Where(node => access.Graph.ShapeOf(node.Index) is { DispatchesRuns: true })
                .Select(node => node.Index)
                .FirstOrDefault();

            return (output, task.Runs.Count);
        });
        int runBaseline = Baseline;

        if (Output is not { } target)
        {
            return [WaitErrors.NoDialogueTarget()];
        }

        ErrorOr<Success> waited = await WaitForAnsweringAsync(task, cancellationToken);
        if (waited.IsError)
        {
            return waited.ErrorsOrEmptyList;
        }

        var reply = new TaskCompletionSource<ErrorOr<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Run? round = null;

        // 订阅先于回答挂上：本轮 run 可能紧接回答就收口，晚挂会漏掉通知
        Action unsubscribeAttached = task.SubscribeRunAttached(run =>
        {
            if (run.Context.NodeIndex != target)
            {
                return;
            }

            if (task.UnderLock(() => task.Runs.TakeWhile(candidate => !ReferenceEquals(candidate, run)).Count() < runBaseline))
            {
                return;
            }

            task.UnderLock(() =>
            {
                round = run;

                return true;
            });

            if (observer is { } sink)
            {
                run.TextReceived += sink.OnText;
            }
        });
        Action unsubscribeSettled = task.SubscribeRunSettled(run =>
        {
            if (run.Context.NodeIndex != target)
            {
                return;
            }

            if (task.UnderLock(() => task.Runs.TakeWhile(candidate => !ReferenceEquals(candidate, run)).Count() < runBaseline))
            {
                return;
            }

            // 收口后不再有增量，解除观察者的流式订阅
            if (observer is { } sink)
            {
                run.TextReceived -= sink.OnText;
            }

            if (run.State == RunState.Succeeded)
            {
                reply.TrySetResult(run.Result ?? string.Empty);
                return;
            }

            reply.TrySetResult(WaitErrors.RoundFailed([.. run.Snapshot().Failures]));
        });
        try
        {
            ErrorOr<Success> answered = waits.Answer(id, nodeName, input);
            if (answered.IsError)
            {
                return answered.ErrorsOrEmptyList;
            }

            while (true)
            {
                if (reply.Task.IsCompleted)
                {
                    if (reply.Task.Result.IsError)
                    {
                        return await reply.Task;
                    }

                    // 回复产出已定，等反馈复位完成、任务回到等待再返回，避免下一轮回答撞上复位窗口；
                    // 任务不再推进时不会再回到等待，本轮回复仍然成立
                    if (task.State != TaskState.Running || Answering(id))
                    {
                        return await reply.Task;
                    }
                }
                else
                {
                    TaskState state = task.State;
                    bool started = task.UnderLock(() => round is not null);

                    // 本轮 run 已挂上时结果由它的收口给出，未挂上才按任务状态判定收口
                    if (!started && state is (TaskState.Blocked or TaskState.Canceled or TaskState.Done))
                    {
                        return [WaitErrors.Settled(id, state)];
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(20, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            task.UnderLock(() =>
            {
                foreach (Run run in task.Runs.Where(run => run.IsLive))
                {
                    run.Cancel();
                }

                return true;
            });

            return WaitErrors.RoundCanceled();
        }
        finally
        {
            unsubscribeAttached();
            unsubscribeSettled();
        }
    }

    /// <summary>等任务停在一个待回答的节点上：提交后推进循环异步把挂点评估出来，过早回答命中不了输入节点。
    /// Done 可能是挂点评估前的瞬态，真收口由 DialogueHost 重建兜底，这里只拦截阻塞与取消。</summary>
    private async Task<ErrorOr<Success>> WaitForAnsweringAsync(WorkTask task, CancellationToken cancellationToken)
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
                return [WaitErrors.Settled(task.Id, state)];
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(20, cancellationToken);
        }
    }

    /// <summary>任务里是否有停在等待回答的节点。</summary>
    private bool Answering(TaskId id) =>
        waits.Pending(id) is { IsError: false } pending
        && pending.Value.Any(node => node.Kind == AwaitKind.Answer);
}
