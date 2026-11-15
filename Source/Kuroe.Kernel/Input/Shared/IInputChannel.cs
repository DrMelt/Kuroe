using Kuroe.Tasks.Shared;

namespace Kuroe.Input.Shared;

/// <summary>流程步骤的输入登记面：步骤声明此刻等待外部输入，交付经接收委托交给步骤。
/// 一次登记对应一次交付，交付后登记撤销。读写要求持有任务 Gate。</summary>
public interface IInputChannel
{
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