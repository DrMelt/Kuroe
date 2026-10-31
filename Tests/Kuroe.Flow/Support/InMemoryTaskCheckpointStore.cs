using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.TestSupport;

/// <summary>内存检查点存储：只实现存储端口契约，供契约测试与后续实现对照。</summary>
public sealed class InMemoryTaskCheckpointStore : ITaskCheckpointStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<TaskId, TaskCheckpointDocument> _documents = [];
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
                [.. _documents.Values.OrderBy(document => document.Task.Identity.Id.Value)],
                [.. _failures]);
        }
    }

    public ErrorOr<Success> Write(TaskCheckpointDocument document)
    {
        lock (_gate)
        {
            _documents[document.Task.Identity.Id] = document;
        }

        return Result.Success;
    }

    public ErrorOr<Success> Delete(TaskId id)
    {
        lock (_gate)
        {
            _documents.Remove(id);
        }

        return Result.Success;
    }
}
