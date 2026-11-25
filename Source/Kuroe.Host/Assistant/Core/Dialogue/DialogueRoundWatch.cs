using ErrorOr;
using Kuroe.Host.Composition.Pending;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Host.Assistant.Dialogue;

/// <summary>一轮对话执行的观察：在同一任务锁内建立边界并订阅挂上与收口，本轮结果在收口时给出。</summary>
internal sealed class DialogueRoundWatch : IDisposable
{
    private readonly WorkTask _task;
    private readonly TaskCompletionSource<ErrorOr<Success>> _outcome =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _boundary;
    private IExecution? _round;
    private Action _unsubscribeAttached = () => { };
    private Action _unsubscribeSettled = () => { };

    private DialogueRoundWatch(WorkTask task) => _task = task;

    /// <summary>本轮执行是否已挂上。</summary>
    internal bool Started => _task.UnderLock(() => _round is not null);

    /// <summary>本轮结果，执行收口时给出。</summary>
    internal Task<ErrorOr<Success>> Outcome => _outcome.Task;

    /// <summary>建立本轮观察：旧执行的通知一律忽略，观察者在接受本轮执行后挂上。</summary>
    internal static DialogueRoundWatch Attach(WorkTask task, string step, ProcessObserver? observer)
    {
        DialogueRoundWatch watch = new(task);

        task.UnderLock(() =>
        {
            watch.Subscribe(step, observer);
            return true;
        });

        return watch;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _unsubscribeAttached();
        _unsubscribeSettled();
    }

    private void Subscribe(string step, ProcessObserver? observer)
    {
        _boundary = _task.Executions.Count == 0
            ? 0
            : _task.Executions.Max(execution => execution.Id.Value);

        _unsubscribeAttached = _task.SubscribeExecutionAttached(execution =>
        {
            if (execution.Id.Value <= _boundary || Uri.UnescapeDataString(execution.Request.Step.Value) != step)
            {
                return;
            }

            bool accepted = _task.UnderLock(() =>
            {
                if (_round is not null)
                {
                    return false;
                }

                _round = execution;
                return true;
            });

            if (!accepted)
            {
                return;
            }

            if (observer?.Content is { } contentObserver)
            {
                execution.ContentEmitted += contentObserver;
            }

            if (observer?.Entry is { } entryObserver)
            {
                execution.EntryEmitted += entryObserver;
            }
        });

        _unsubscribeSettled = _task.SubscribeExecutionSettled(execution =>
        {
            if (execution.Id.Value <= _boundary || Uri.UnescapeDataString(execution.Request.Step.Value) != step)
            {
                return;
            }

            if (!_task.UnderLock(() => ReferenceEquals(_round, execution)))
            {
                return;
            }

            if (execution.State == ExecutionState.Succeeded)
            {
                _outcome.TrySetResult(Result.Success);
                return;
            }

            _outcome.TrySetResult(PendingErrors.RoundFailed([.. execution.Snapshot().Failures]));
        });
    }
}