using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Persistence.Saving.Shared;
using Kuroe.Flow.Node.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.Saving;

/// <summary>存档写盘协调：合并状态变化，后台逐任务捕获并写入，刷新时等待全部或指定任务落盘。</summary>
public sealed class TaskRecordCoordinator : ITaskRecordCoordinator
{
    private readonly Lock _gate = new();
    private readonly SemaphoreSlim _write = new(1, 1);
    private readonly HashSet<TaskId> _dirty = [];
    private readonly HashSet<TaskId> _removed = [];
    private readonly ITaskRecordSource _source;
    private readonly ITaskRecordStore _store;
    private bool _running;

    /// <summary>按捕获源与存储建立协调器，任务状态变化在此接上存档写盘。</summary>
    public TaskRecordCoordinator(
        ITaskRecordSource source,
        ITaskRecordStore store)
    {
        _source = source;
        _store = store;
        source.Changed += MarkDirty;
    }

    /// <inheritdoc/>
    public void MarkDirty(TaskId id)
    {
        bool start;
        lock (_gate)
        {
            _dirty.Add(id);
            start = !_running;
            _running = true;
        }

        if (start)
        {
            _ = Task.Run(FlushInBackgroundAsync);
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync()
    {
        await _write.WaitAsync().ConfigureAwait(false);
        try
        {
            Drain();
        }
        catch
        {
            // 写盘中断后保留待写标记，允许后续变化重新调度
            Halt();

            throw;
        }
        finally
        {
            _write.Release();
        }
    }

    /// <inheritdoc/>
    public async Task FlushAsync(TaskId id)
    {
        MarkDirty(id);

        await _write.WaitAsync().ConfigureAwait(false);
        try
        {
            bool pending;
            lock (_gate)
            {
                pending = _dirty.Remove(id);
            }

            if (pending && !Write(id))
            {
                throw new InvalidOperationException($"任务 {id} 的存档写入失败。");
            }
        }
        catch
        {
            Halt();

            throw;
        }
        finally
        {
            _write.Release();
        }
    }

    /// <inheritdoc/>
    public void Remove(TaskId id)
    {
        _write.Wait();
        try
        {
            lock (_gate)
            {
                _dirty.Remove(id);
                _removed.Add(id);
            }

            _ = _store.Delete(id);
        }
        finally
        {
            _write.Release();
        }
    }

    /// <summary>写盘全部待写任务；写入期间的新变化留在下一轮。写失败保留待写标记并结束本轮，等下一次变化或刷新重试。</summary>
    private void Drain()
    {
        while (true)
        {
            TaskId next;
            lock (_gate)
            {
                if (_dirty.Count == 0)
                {
                    _running = false;
                    return;
                }

                next = _dirty.First();
                _dirty.Remove(next);
            }

            if (IsRemoved(next))
            {
                continue;
            }

            if (!Write(next))
            {
                return;
            }
        }
    }

    /// <summary>写一个任务；写失败时保留待写标记并结束本轮，返回 false。</summary>
    private bool Write(TaskId id)
    {
        TaskRecordCaptureResult captured;
        try
        {
            captured = _source.Capture(id);
        }
        catch
        {
            Retry(id);
            throw;
        }

        if (!captured.Found)
        {
            return true;
        }

        if (captured.Record.IsError)
        {
            Retry(id);
            Halt();
            return false;
        }

        ErrorOr<Success> written;
        try
        {
            written = _store.Write(captured.Record.Value);
        }
        catch
        {
            Retry(id);
            throw;
        }

        if (written.IsError)
        {
            Retry(id);
            Halt();
            return false;
        }

        return true;
    }

    /// <summary>后台写盘：异常已中断本轮并保留待写标记，这里只防止未观察异常。</summary>
    private async Task FlushInBackgroundAsync()
    {
        try
        {
            await FlushAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 写盘中断后保留待写标记，调度位复位以允许后续变化重新调度
            Halt();
        }
    }

    private void Halt()
    {
        lock (_gate)
        {
            _running = false;
        }
    }

    private bool IsRemoved(TaskId id)
    {
        lock (_gate)
        {
            return _removed.Contains(id);
        }
    }

    /// <summary>写盘失败时保留待写标记，等待下一次变化或刷新重试。</summary>
    private void Retry(TaskId id)
    {
        lock (_gate)
        {
            if (!_removed.Contains(id))
            {
                _dirty.Add(id);
            }
        }
    }
}
