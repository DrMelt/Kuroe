using ErrorOr;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Persistence.FileSystem;

/// <summary>任务租约的文件实现：按任务键打开独占文件句柄，进程退出时由系统释放。</summary>
public sealed class FileTaskLeaseStore(string directory) : ITaskLeaseStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FileTaskLease> _held = [];

    /// <inheritdoc/>
    public ErrorOr<ITaskLease> TryAcquire(TaskId id)
    {
        lock (_gate)
        {
            if (_held.ContainsKey(id.Value))
            {
                return [Error.Conflict(FlowErrorCodes.TaskLease, $"{id} 已被本进程持有。")];
            }

            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, $"{id.Value}.lock");
                FileTaskLease lease = new(
                    id,
                    new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None),
                    () => Release(id.Value));

                _held[id.Value] = lease;

                return lease;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return [Error.Conflict(FlowErrorCodes.TaskLease, $"{id} 正被其他进程使用：{ex.Message}")];
            }
        }
    }

    /// <summary>释放时从持有表移除，锁文件保留。</summary>
    private void Release(string key)
    {
        lock (_gate)
        {
            _held.Remove(key);
        }
    }
}

/// <summary>一个文件句柄给出的任务租约。</summary>
internal sealed class FileTaskLease(TaskId id, FileStream stream, Action release) : ITaskLease
{
    private int _disposed;

    public TaskId Id => id;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        stream.Dispose();
        release();
    }
}
