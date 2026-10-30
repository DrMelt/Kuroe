using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Shared;

/// <summary>任务检查点的持久化端口。存储负责路径、枚举、原子写入与删除。</summary>
public interface ITaskCheckpointStore
{
    /// <summary>读取全部检查点，按任务号顺序。单个文件失败经结果中的失败项报告。</summary>
    TaskStoreReadResult ReadAll();

    /// <summary>写入一个任务检查点，同一任务号覆盖旧文件。</summary>
    ErrorOr<Success> Write(TaskCheckpointDocument document);

    /// <summary>删除一个任务的检查点。任务不存在时视为成功。</summary>
    ErrorOr<Success> Delete(TaskId id);
}

/// <summary>一次读取的结果：成功读取的文档与逐文件失败。</summary>
public sealed record TaskStoreReadResult(
    IReadOnlyList<TaskCheckpointDocument> Documents,
    IReadOnlyList<TaskStoreFailure> Failures);

/// <summary>一个检查点文件的读取失败。</summary>
public sealed record TaskStoreFailure(
    string Source,
    IReadOnlyList<Error> Errors);
