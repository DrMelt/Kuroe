using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;

namespace Kuroe.Flow.TestSupport;

/// <summary>任务观察面：由状态变化驱动等待。
/// 条件满足时返回当时快照；等待时限只用于通知丢失时把挂起变成失败。</summary>
public interface ITaskWaiter
{
    /// <summary>等到任务满足条件，返回当时的快照。</summary>
    TaskSnapshot Wait(TaskId id, TaskPredicate condition);

    /// <summary>等到任务满足条件，返回当时的快照。</summary>
    Task<TaskSnapshot> WaitAsync(TaskId id, TaskPredicate condition);

    /// <summary>等到任务不再自动推进：已完成、阻塞、终止或暂停，且没有在跑执行。停在等待节点不构成收口。</summary>
    TaskSnapshot Settle(TaskId id);

    /// <summary>等到任务不再自动推进：已完成、阻塞、终止或暂停，且没有在跑执行。停在等待节点不构成收口。</summary>
    Task<TaskSnapshot> SettleAsync(TaskId id);
}