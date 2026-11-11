using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>内存存档存储：只实现存储端口契约，供契约测试与后续实现对照。</summary>
public sealed class InMemoryTaskRecordStore : ITaskRecordStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, TaskRecord> _records = [];
    private readonly List<TaskStoreFailure> _failures = [];

    /// <summary>注入一条读取失败，下一次读取随结果返回。</summary>
    public void AddFailure(TaskStoreFailure failure)
    {
        lock (_gate)
        {
            _failures.Add(failure);
        }
    }

    public TaskStoreReadResult ReadAll()
    {
        lock (_gate)
        {
            return new TaskStoreReadResult(
                [.. _records.Values.OrderBy(record => record.Identity.Id.Value)],
                [.. _failures]);
        }
    }

    public ErrorOr<Success> Write(TaskRecord record)
    {
        lock (_gate)
        {
            _records[record.Identity.Id] = record;
        }

        return Result.Success;
    }

    public ErrorOr<Success> Delete(TaskId id)
    {
        lock (_gate)
        {
            _records.Remove(id);
        }

        return Result.Success;
    }
}
