using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Input.Shared;

/// <summary>流程步骤的输入登记面：步骤声明此刻等待外部输入，交付经接收委托交给步骤。
/// 一次登记对应一次交付，交付后登记撤销。Accept 与 Decline 可在任务 Gate 外调用，登记表内部同步；任务清理后登记被拒绝。</summary>
public interface IInputChannel
{
    /// <summary>等待登记发生变化，宿主据此刷新任务快照。</summary>
    event Action<TaskId>? Changed;

    /// <summary>登记一个等待外部输入的步骤。重复登记同一步骤时替换原登记。</summary>
    void Accept(
        TaskId task,
        FlowStepPath step,
        FlowPendingKind kind,
        string? question,
        InputReceiver receive);

    /// <summary>撤销一个步骤的等待登记。</summary>
    void Decline(TaskId task, FlowStepPath step);
}