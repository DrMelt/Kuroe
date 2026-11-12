using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务状态变化的播发面。观察方据此重新读取快照。
/// 事件可能在任务锁内触发，处理者只做唤醒，不读快照。</summary>
public interface ITaskChangeSource
{
    /// <summary>任务状态已变，给出任务号。</summary>
    event Action<TaskId>? Changed;
}