using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务索引的只读查询面。信息查询工具依赖此契约，任务域实现提供快照。</summary>
public interface ITaskReader
{
    /// <summary>按任务键取任务快照，不存在时返回错误。</summary>
    ErrorOr<TaskSnapshot> Find(TaskId id);

    /// <summary>按完整键或末尾短键取任务快照，不存在或歧义时返回错误。</summary>
    ErrorOr<TaskSnapshot> FindByReference(string reference);

    /// <summary>按执行号取执行快照，不存在或不属于本任务域时返回错误。</summary>
    ErrorOr<ExecutionSnapshot> FindExecution(ExecutionId id);

    /// <summary>本域全部任务的快照，按创建时间。</summary>
    IReadOnlyList<TaskSnapshot> Snapshots();
}