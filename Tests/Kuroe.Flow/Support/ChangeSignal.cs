namespace Kuroe.Flow.TestSupport;

/// <summary>版本号加唤醒的变更信号：等待方先记版本，判定条件，再等待版本变化，避免判定与挂起之间漏唤醒。</summary>
internal sealed class ChangeSignal
{
    private readonly Lock _gate = new();
    private readonly List<TaskCompletionSource> _waiters = [];
    private long _version;

    /// <summary>当前版本号。</summary>
    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    /// <summary>播发一次变化，唤醒全部等待方。</summary>
    public void Raise()
    {
        TaskCompletionSource[] waiters;
        lock (_gate)
        {
            _version++;
            waiters = [.. _waiters];
            _waiters.Clear();
        }

        foreach (TaskCompletionSource waiter in waiters)
        {
            waiter.TrySetResult();
        }
    }

    /// <summary>等到版本号离开给定值。</summary>
    public async Task WaitAsync(long version)
    {
        TaskCompletionSource waiter;
        lock (_gate)
        {
            if (_version != version)
            {
                return;
            }

            waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(waiter);
        }

        await waiter.Task.ConfigureAwait(false);
    }
}