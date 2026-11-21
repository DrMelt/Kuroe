using ErrorOr;
using Kuroe.Kernel.Tasks.Persistence.Files.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.TestSupport;

/// <summary>内存任务租约：同一目录的多个存储共享持有集合，模拟跨进程的句柄互斥。</summary>
public sealed class InMemoryTaskLeaseStores
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, HashSet<string>> _held = new(StringComparer.Ordinal);

    /// <summary>按存档目录建立租约存储，同目录的存储互斥。</summary>
    public ITaskLeaseStore Create(string directory) => new Store(this, Path.GetFullPath(directory));

    private HashSet<string> Held(string directory)
    {
        if (!_held.TryGetValue(directory, out HashSet<string>? held))
        {
            held = [];
            _held[directory] = held;
        }

        return held;
    }

    private sealed class Store(InMemoryTaskLeaseStores owner, string directory) : ITaskLeaseStore
    {
        private readonly HashSet<string> _mine = [];

        public ErrorOr<Success> TryAcquire(TaskId id)
        {
            lock (owner._gate)
            {
                if (!owner.Held(directory).Add(id.Value))
                {
                    return [Error.Conflict(FlowErrorCodes.TaskLease, $"{id} 正被其他会话使用。")];
                }

                _mine.Add(id.Value);

                return Result.Success;
            }
        }

        public void Release(TaskId id)
        {
            lock (owner._gate)
            {
                if (_mine.Remove(id.Value))
                {
                    owner.Held(directory).Remove(id.Value);
                }
            }
        }

        public void ReleaseAll()
        {
            lock (owner._gate)
            {
                HashSet<string> held = owner.Held(directory);
                foreach (string id in _mine)
                {
                    held.Remove(id);
                }

                _mine.Clear();
            }
        }
    }
}