using ErrorOr;
using Kuroe.Flow.Persistence.Files.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Persistence.Files;

/// <summary>任务租约的文件实现：按任务键打开独占文件句柄，进程退出时由系统释放。锁文件保留。</summary>
public sealed class FileTaskLeaseStore(string directory) : ITaskLeaseStore, IDisposable
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FileStream> _held = [];

    /// <inheritdoc/>
    public ErrorOr<Success> TryAcquire(TaskId id)
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
                _held[id.Value] = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                return Result.Success;
            }
            catch (UnauthorizedAccessException ex)
            {
                return [Error.Failure(FlowErrorCodes.TaskLease, $"无法建立任务锁 {id}：{ex.Message}")];
            }
            catch (IOException ex) when (IsSharingViolation(ex))
            {
                return [Error.Conflict(FlowErrorCodes.TaskLease, $"{id} 正被其他进程使用：{ex.Message}")];
            }
            catch (IOException ex)
            {
                return [Error.Failure(FlowErrorCodes.TaskLease, $"无法建立任务锁 {id}：{ex.Message}")];
            }
        }
    }

    /// <inheritdoc/>
    public void Release(TaskId id)
    {
        lock (_gate)
        {
            if (_held.Remove(id.Value, out FileStream? stream))
            {
                stream.Dispose();
            }
        }
    }

    /// <inheritdoc/>
    public void ReleaseAll()
    {
        lock (_gate)
        {
            foreach (FileStream stream in _held.Values)
            {
                stream.Dispose();
            }

            _held.Clear();
        }
    }

    /// <summary>释放本进程持有的全部租约。</summary>
    public void Dispose() => ReleaseAll();

    /// <summary>句柄冲突：Windows 为共享冲突与锁定冲突，Unix 为 EWOULDBLOCK。</summary>
    private static bool IsSharingViolation(IOException ex)
    {
        int code = ex.HResult & 0xFFFF;

        return OperatingSystem.IsWindows() ? code is 0x20 or 0x21 : code is 11 or 35;
    }
}
