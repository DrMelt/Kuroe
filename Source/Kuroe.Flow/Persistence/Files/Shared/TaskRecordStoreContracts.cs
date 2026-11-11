using ErrorOr;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Files.Shared;

/// <summary>任务存档的持久化端口。</summary>
public interface ITaskRecordStore
{
    /// <summary>读取全部存档，按任务号顺序。单项失败经结果中的失败项报告。</summary>
    TaskStoreReadResult ReadAll();

    /// <summary>写入一个任务存档，同一任务号覆盖旧内容。</summary>
    ErrorOr<Success> Write(TaskRecord record);

    /// <summary>删除一个任务的存档。任务不存在时视为成功。</summary>
    ErrorOr<Success> Delete(TaskId id);
}

/// <summary>一次读取的结果：成功读取的存档与逐项失败。</summary>
public sealed record TaskStoreReadResult(
    IReadOnlyList<TaskRecord> Records,
    IReadOnlyList<TaskStoreFailure> Failures);

/// <summary>一个存档的读取失败，来源由存储标识。</summary>
public sealed record TaskStoreFailure(
    string Source,
    IReadOnlyList<Error> Errors);
