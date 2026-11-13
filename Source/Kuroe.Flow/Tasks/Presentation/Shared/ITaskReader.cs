using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Tasks.Presentation.Shared;

/// <summary>任务索引的只读查询面。信息查询工具依赖此契约，任务域实现提供快照。</summary>
public interface ITaskReader
{
    /// <summary>按任务键取任务快照，不存在时返回错误。</summary>
    ErrorOr<TaskSnapshot> Find(TaskId id);

    /// <summary>按完整键或末尾短键取任务快照，不存在或歧义时返回错误。</summary>
    ErrorOr<TaskSnapshot> FindByReference(string reference);

    /// <summary>按执行号取执行快照，不存在或不属于本任务域时返回错误。</summary>
    ErrorOr<ExecutionSnapshot> FindExecution(ExecutionId id);

    /// <summary>按执行号取执行与归属任务的快照，不存在或不属于本任务域时返回错误。</summary>
    ErrorOr<(TaskSnapshot Task, ExecutionSnapshot Execution)> FindOwnedExecution(ExecutionId id);

    /// <summary>本域在跑的执行，含排队中的，按启动先后。</summary>
    IReadOnlyList<ExecutionSnapshot> LiveExecutions();

    /// <summary>本域全部任务的快照，按创建时间。</summary>
    IReadOnlyList<TaskSnapshot> Snapshots();
}