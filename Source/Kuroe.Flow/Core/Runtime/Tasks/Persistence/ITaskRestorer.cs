using ErrorOr;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks.Persistence;

/// <summary>从存档恢复任务。实现负责构造任务并恢复执行、节点与过程记录。</summary>
public interface ITaskRestorer
{
    /// <summary>按存档与编译后的流程恢复任务。恢复后任务尚未加入任务域。</summary>
    ErrorOr<WorkTask> Restore(TaskRecord record, CompiledFlow flow);
}
