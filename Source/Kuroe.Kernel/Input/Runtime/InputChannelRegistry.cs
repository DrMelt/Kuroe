using ErrorOr;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Input.Runtime;

/// <summary>输入通道的进程内实现：按任务与步骤保存等待登记、提问与接收委托。
/// 内部锁只保护登记表结构，接收委托在锁外调用，调用方须持有任务 Gate。</summary>
public sealed class InputChannelRegistry : IInputChannel, IInputDelivery
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, Dictionary<FlowStepPath, Acceptance>> _tasks = [];

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
            if (!_tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps))
            {
                steps = [];
                _tasks[task] = steps;
            }

            steps[step] = new Acceptance(kind, question, receive);
        }
    }

    /// <inheritdoc/>
    public void Decline(TaskId task, FlowStepPath step)
    {
        lock (_gate)
        {
            if (!_tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps))
            {
                return;
            }

            steps.Remove(step);
            if (steps.Count == 0)
            {
                _tasks.Remove(task);
            }
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<FlowPendingStep> Pending(TaskId task)
    {
        lock (_gate)
        {
            return _tasks.TryGetValue(task, out Dictionary<FlowStepPath, Acceptance>? steps)
                ? [.. steps.Select(entry => new FlowPendingStep(entry.Key, entry.Value.Kind, entry.Value.Question))]
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
        }
    }

    private readonly record struct Acceptance(FlowPendingKind Kind, string? Question, InputReceiver Receive);
}