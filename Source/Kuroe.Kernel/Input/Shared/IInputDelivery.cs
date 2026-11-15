using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Input.Shared;

/// <summary>输入登记的宿主面：读取任务此刻等待的步骤、交付一次输入并清理任务登记。</summary>
public interface IInputDelivery
{
    /// <summary>任务里此刻等待外部输入的步骤。</summary>
    IReadOnlyList<FlowPendingStep> Pending(TaskId task);

    /// <summary>向等待中的步骤交付一次输入并撤销登记。要求持有任务 Gate。</summary>
    ErrorOr<Success> Deliver(TaskId task, FlowStepPath step, string input);

    /// <summary>清空一个任务的全部等待登记。</summary>
    void Remove(TaskId task);
}