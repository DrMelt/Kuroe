using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Runner.Approval.Tests;

/// <summary>测试用的输入通道替身：记录声明与撤销，供用例断言。</summary>
internal sealed class StubInputChannel : IInputChannel
{
    /// <inheritdoc/>
    public event Action<TaskId>? Changed;

    /// <summary>收到的声明，按声明顺序。</summary>
    public List<(FlowStepPath Step, FlowPendingKind Kind, string? Question, InputReceiver Receive)> Declared { get; } = [];

    /// <summary>收到的撤销，按撤销顺序。</summary>
    public List<FlowStepPath> Declined { get; } = [];

    /// <summary>登记一个等待外部输入的步骤。</summary>
    public void Accept(
        TaskId task,
        FlowStepPath step,
        FlowPendingKind kind,
        string? question,
        InputReceiver receive)
    {
        Declared.Add((step, kind, question, receive));
        Changed?.Invoke(task);
    }

    /// <summary>撤销一个步骤的等待登记。</summary>
    public void Decline(TaskId task, FlowStepPath step)
    {
        Declined.Add(step);
        Changed?.Invoke(task);
    }
}