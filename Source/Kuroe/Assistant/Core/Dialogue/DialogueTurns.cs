using ErrorOr;
using Kuroe.Tasks.Runtime;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>会话轮次：同一会话同一时刻只允许一轮，轮次结束给出通知。</summary>
internal sealed class DialogueTurns(TaskRegistry registry, DialogueRound rounds)
{
    private const int IdleWaitAttempts = 250;
    private static readonly TimeSpan IdleWaitInterval = TimeSpan.FromMilliseconds(20);

    private readonly Lock _gate = new();
    private readonly HashSet<TaskId> _running = [];

    /// <summary>一轮结束触发，宿主据此给出完成提示。</summary>
    internal event Action<DialogueTurnNotice>? Completed;

    /// <summary>会话是否有一轮没有收口。</summary>
    internal bool IsRunning(TaskId id)
    {
        lock (_gate)
        {
            return _running.Contains(id);
        }
    }

    /// <summary>会话是否忙：有一轮没有收口，或还有执行在跑。</summary>
    internal bool IsBusy(TaskId id) =>
        IsRunning(id) || (registry.Find(id) is { IsError: false } found && IsLive(found.Value));

    /// <summary>把一轮输入交给会话。会话正在执行时拒绝，不排队。</summary>
    internal async Task<ErrorOr<Success>> SendAsync(
        WorkTask task,
        string input,
        ProcessObserver? observer)
    {
        if (!TryStart(task.Id))
        {
            return [DialogueErrors.Busy(task.Id)];
        }

        try
        {
            if (IsLive(task))
            {
                return [DialogueErrors.Busy(task.Id)];
            }

            ErrorOr<Success> outcome = await rounds.AskAsync(task.Id, input, observer).ConfigureAwait(false);
            Completed?.Invoke(new DialogueTurnNotice(
                task.Id,
                outcome.IsError ? outcome.ErrorsOrEmptyList : []));

            return outcome;
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(task.Id);
            }
        }
    }

    /// <summary>等会话既没有轮次也没有在跑的执行，超时视为仍未空闲。</summary>
    internal async Task<bool> WaitIdleAsync(WorkTask task, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt < IdleWaitAttempts; attempt++)
        {
            if (IsIdle(task))
            {
                return true;
            }

            await Task.Delay(IdleWaitInterval, cancellationToken).ConfigureAwait(false);
        }

        return IsIdle(task);
    }

    private bool TryStart(TaskId id)
    {
        lock (_gate)
        {
            return _running.Add(id);
        }
    }

    private bool IsIdle(WorkTask task) => !IsRunning(task.Id) && !IsLive(task);

    private static bool IsLive(WorkTask task) =>
        task.UnderLock(() => task.Executions.Any(execution => execution.IsLive));
}