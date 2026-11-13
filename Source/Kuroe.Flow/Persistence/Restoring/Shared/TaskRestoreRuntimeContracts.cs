using ErrorOr;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Persistence.Restoring.Shared;

/// <summary>从存档恢复任务的运行时端口。由运行时实现任务构造、加入任务域与推进循环恢复。</summary>
public interface ITaskRestoreRuntime
{
    /// <summary>按存档恢复任务，并返回恢复后的任务快照。</summary>
    ErrorOr<TaskSnapshot> Restore(TaskRecord record);
}
