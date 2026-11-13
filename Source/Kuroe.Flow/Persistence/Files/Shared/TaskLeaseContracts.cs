using ErrorOr;
using Kuroe.Flow.Node.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Files.Shared;

/// <summary>任务级租约：同一任务同一时刻只允许一个进程持有。持有集合由实现维护，调用方只登记与注销。</summary>
public interface ITaskLeaseStore
{
    /// <summary>获取任务租约并登记为已持有。已被其他进程或本进程持有时返回 Conflict，其它 IO 故障返回 Failure。</summary>
    ErrorOr<Success> TryAcquire(TaskId id);

    /// <summary>注销任务租约并关闭句柄，锁文件保留。未持有时忽略。</summary>
    void Release(TaskId id);

    /// <summary>注销本进程持有的全部租约并关闭句柄，锁文件保留。</summary>
    void ReleaseAll();
}
