using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务观察面：由状态变化驱动等待。
/// 条件满足时返回当时快照；等待时限只用于通知丢失时把挂起变成失败。</summary>
public interface ITaskWaiter
{
    /// <summary>等到任务满足条件，返回当时的快照。</summary>
    TaskSnapshot Wait(TaskId id, TaskPredicate condition);

    /// <summary>等到任务满足条件，返回当时的快照。</summary>
    Task<TaskSnapshot> WaitAsync(TaskId id, TaskPredicate condition);

    /// <summary>等到推进循环没有在跑的活：到终态、阻塞、暂停或停在等待外部输入。</summary>
    TaskSnapshot Settle(TaskId id);

    /// <summary>等到推进循环没有在跑的活：到终态、阻塞、暂停或停在等待外部输入。</summary>
    Task<TaskSnapshot> SettleAsync(TaskId id);
}