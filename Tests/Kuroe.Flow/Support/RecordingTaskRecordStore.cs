using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.TestSupport;

/// <summary>内存存档存储加写入记录：每次写入成功后记下序号与内容，并唤醒等待方。</summary>
public sealed class RecordingTaskRecordStore : ITaskRecordRecordingStore
{
    private readonly Lock _gate = new();
    private readonly InMemoryTaskRecordStore _inner = new();
    private readonly List<TaskRecordWrite> _writes = [];
    private readonly List<Waiter> _waiters = [];
    private int _sequence;

    public IReadOnlyList<TaskRecordWrite> Writes
    {
        get
        {
            lock (_gate)
            {
                return [.. _writes];
            }
        }
    }

    public TaskStoreReadResult ReadAll() => _inner.ReadAll();

    public ErrorOr<Success> Write(TaskRecord record)
    {
        ErrorOr<Success> written = _inner.Write(record);
        if (written.IsError)
        {
            return written;
        }

        TaskRecordWrite write;
        List<Waiter> ready = [];
        lock (_gate)
        {
            write = new TaskRecordWrite(++_sequence, record);
            _writes.Add(write);
            for (int i = _waiters.Count - 1; i >= 0; i--)
            {
                if (_waiters[i].Condition(record))
                {
                    ready.Add(_waiters[i]);
                    _waiters.RemoveAt(i);
                }
            }
        }

        foreach (Waiter waiter in ready)
        {
            waiter.Completion.TrySetResult(write);
        }

        return written;
    }

    public ErrorOr<Success> Delete(TaskId id) => _inner.Delete(id);

    public Task<TaskRecordWrite> NextWrite(Func<TaskRecord, bool> condition) => Next(condition);

    private async Task<TaskRecordWrite> Next(Func<TaskRecord, bool> condition)
    {
        Task<TaskRecordWrite> write;
        lock (_gate)
        {
            foreach (TaskRecordWrite recorded in _writes)
            {
                if (condition(recorded.Record))
                {
                    return recorded;
                }
            }

            Waiter waiter = new(condition);
            _waiters.Add(waiter);
            write = waiter.Completion.Task;
        }

        try
        {
            return await write.WaitAsync(TestTimeouts.HangGuard).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"存档在 {TestTimeouts.HangGuard.TotalSeconds} 秒内没有出现满足条件的写入。");
        }
    }

    private sealed class Waiter(Func<TaskRecord, bool> condition)
    {
        /// <summary>该等待方认可的写入条件。</summary>
        public Func<TaskRecord, bool> Condition { get; } = condition;

        /// <summary>条件命中时完成的写入结果。</summary>
        public TaskCompletionSource<TaskRecordWrite> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}