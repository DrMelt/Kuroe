using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>一个任务的独占租约，持有期间其他进程不能接管。</summary>
public interface ITaskLease : IDisposable
{
    /// <summary>租约对应的任务键。</summary>
    TaskId Id { get; }
}

/// <summary>任务级租约：同一任务同一时刻只允许一个进程持有。</summary>
public interface ITaskLeaseStore
{
    /// <summary>尝试获取任务租约。被其他进程持有时返回冲突错误。</summary>
    ErrorOr<ITaskLease> TryAcquire(TaskId id);
}
