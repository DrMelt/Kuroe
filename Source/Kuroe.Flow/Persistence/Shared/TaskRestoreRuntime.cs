using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>从检查点恢复任务的运行时端口。由运行时实现任务构造、加入任务域与推进循环恢复。</summary>
public interface ITaskRestoreRuntime
{
    /// <summary>按检查点与编译后的流程恢复任务，并返回恢复后的任务快照。</summary>
    ErrorOr<TaskSnapshot> Restore(
        TaskCheckpoint checkpoint,
        CompiledFlow flow,
        IReadOnlyList<NodePath> seed);
}
