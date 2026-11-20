using ErrorOr;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Input.Runtime;

/// <summary>输入通道的进程内实现：按任务与步骤保存等待登记、提问与接收委托。
/// 内部锁保护登记表结构，接收委托在锁外调用。交付要求持有任务 Gate；登记可在 Gate 外调用，任务清理后拒绝新登记。</summary>
public sealed class InputChannelRegistry : IInputChannel, IInputDelivery
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, Dictionary<FlowStepPath, Acceptance>> _tasks = [];
    private readonly HashSet<TaskId> _closed = [];

    /// <inheritdoc/>
    public event Action<TaskId>? Changed;

    /// <inheritdoc/>
    public void Accept(
        TaskId task,
        FlowStepPath step,
        FlowPendingKind kind,
        string? question,
        InputReceiver receive)
    {
        lock (_gate)
        {
            if (_closed.Contains(task))
            {
                return;
            }

            if (!_tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps))
            {
                steps = [];
                _tasks[task] = steps;
            }

            steps[step] = new Acceptance(kind, question, receive);
        }

        Changed?.Invoke(task);
    }

    /// <inheritdoc/>
    public void Decline(TaskId task, FlowStepPath step)
    {
        bool removed;
        lock (_gate)
        {
            if (!_tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps))
            {
                return;
            }

            removed = steps.Remove(step);
            if (steps.Count == 0)
            {
                _tasks.Remove(task);
            }
        }

        if (removed)
        {
            Changed?.Invoke(task);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<FlowPendingStep> Pending(TaskId task)
    {
        lock (_gate)
        {
            return _tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps)
                ? [.. steps
                    .OrderBy(entry => entry.Key.Value, StringComparer.Ordinal)
                    .Select(entry => new FlowPendingStep(entry.Key, entry.Value.Kind, entry.Value.Question))]
                : [];
        }
    }

    /// <inheritdoc/>
    public ErrorOr<Success> Deliver(TaskId task, FlowStepPath step, string input)
    {
        Acceptance acceptance;
        lock (_gate)
        {
            if (!_tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps)
                || !steps.Remove(step, out acceptance))
            {
                return [InputErrors.StepNotAwaiting(task, step)];
            }

            if (steps.Count == 0)
            {
                _tasks.Remove(task);
            }
        }

        acceptance.Receive(input);

        return Result.Success;
    }

    /// <inheritdoc/>
    public void Remove(TaskId task)
    {
        lock (_gate)
        {
            _tasks.Remove(task);
            _closed.Add(task);
        }
    }

    private readonly record struct Acceptance(FlowPendingKind Kind, string? Question, InputReceiver Receive);
}