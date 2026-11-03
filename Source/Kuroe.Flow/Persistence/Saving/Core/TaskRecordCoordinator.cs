using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Saving;

/// <summary>存档写盘协调：合并变更标记，刷新时逐任务捕获并写入存储。任务走完时立即写终态。</summary>
public sealed class TaskRecordCoordinator : ITaskRecordCoordinator
{
    private readonly Lock _gate = new();
    private readonly Lock _flush = new();
    private readonly Dictionary<TaskId, int> _generations = [];
    private readonly ITaskRecordSource _source;
    private readonly ITaskRecordStore _store;
    private int _generation;

    /// <summary>按捕获源与存储建立协调器，任务走完的通知在此接上终态写盘。</summary>
    public TaskRecordCoordinator(
        ITaskRecordSource source,
        ITaskRecordStore store)
    {
        _source = source;
        _store = store;
        source.Completed += OnCompleted;
    }

    /// <inheritdoc/>
    public void MarkDirty(TaskId id)
    {
        lock (_gate)
        {
            _generations[id] = ++_generation;
        }
    }

    /// <inheritdoc/>
    public Task FlushAsync()
    {
        // 串行刷新：后开始的捕获一定后写，旧快照不会覆盖新快照
        lock (_flush)
        {
            FlushCore();
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void Remove(TaskId id)
    {
        // 与刷新串行：等待在途写入结束后再删除，删除后不会再有陈旧快照落盘
        lock (_flush)
        {
            lock (_gate)
            {
                _generations.Remove(id);
            }

            _ = _store.Delete(id);
        }
    }

    private void FlushCore()
    {
        KeyValuePair<TaskId, int>[] batch;
        lock (_gate)
        {
            batch = [.. _generations];
            _generations.Clear();
        }

        foreach ((TaskId id, int generation) in batch)
        {
            TaskRecordCaptureResult captured = _source.Capture(id);
            if (!captured.Found)
            {
                continue;
            }

            if (captured.Record.IsError)
            {
                Retry(id, generation);
                continue;
            }

            ErrorOr<Success> written = _store.Write(captured.Record.Value);
            if (written.IsError)
            {
                Retry(id, generation);
            }
        }
    }

    /// <summary>任务走完时立即写终态，进程随时退出都能留下完成事实。</summary>
    private void OnCompleted(TaskId id)
    {
        MarkDirty(id);
        _ = FlushAsync();
    }

    /// <summary>写盘失败时保留变更标记，等待下一次刷新；期间已有新变更时不回退代数。</summary>
    private void Retry(TaskId id, int generation)
    {
        lock (_gate)
        {
            if (!_generations.TryGetValue(id, out int current) || current < generation)
            {
                _generations[id] = generation;
            }
        }
    }
}
