using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.TestSupport;

/// <summary>按任务状态变化唤醒的观察面：读快照用只读面，唤醒用变更播发面。</summary>
public sealed class NotifiedTaskWaiter : ITaskWaiter, IDisposable
{
    /// <summary>通知丢失时结束等待的上限，只用于把挂起变成失败。</summary>
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(60);

    private readonly ITaskReader _reader;
    private readonly ITaskChangeSource _changes;
    private readonly Func<TaskId, bool> _parkedAt;
    private readonly ChangeSignal _signal = new();

    /// <summary>按只读面与变更播发面建立观察，parkedAt 给出任务是否停在等待外部输入。</summary>
    public NotifiedTaskWaiter(ITaskReader reader, ITaskChangeSource changes, Func<TaskId, bool> parkedAt)
    {
        _reader = reader;
        _changes = changes;
        _parkedAt = parkedAt;
        changes.Changed += OnChanged;
    }

    public TaskSnapshot Wait(TaskId id, TaskPredicate condition) =>
        WaitAsync(id, condition).GetAwaiter().GetResult();

    public async Task<TaskSnapshot> WaitAsync(TaskId id, TaskPredicate condition)
    {
        while (true)
        {
            long version = _signal.Version;
            TaskSnapshot snapshot = Read(id);
            if (condition(snapshot))
            {
                return snapshot;
            }

            await NextChangeAsync(id, version, snapshot).ConfigureAwait(false);
        }
    }

    public TaskSnapshot Settle(TaskId id) => SettleAsync(id).GetAwaiter().GetResult();

    public async Task<TaskSnapshot> SettleAsync(TaskId id)
    {
        while (true)
        {
            long version = _signal.Version;
            TaskSnapshot snapshot = Read(id);
            if (Settled(id, snapshot))
            {
                return snapshot;
            }

            await NextChangeAsync(id, version, snapshot).ConfigureAwait(false);
        }
    }

    /// <summary>推进循环已没有可推进的活：没有在跑执行，且已到终态或停在等待外部输入。</summary>
    private bool Settled(TaskId id, TaskSnapshot snapshot) =>
        snapshot.LiveExecutions == 0
        && (snapshot.State is TaskState.Done or TaskState.Blocked or TaskState.Terminated or TaskState.Paused
            || _parkedAt(id));

    private TaskSnapshot Read(TaskId id) => _reader.Find(id).ThrowIfError();

    /// <summary>等下一次任务变化，超时说明通知缺失。</summary>
    private async Task NextChangeAsync(TaskId id, long version, TaskSnapshot snapshot)
    {
        try
        {
            await _signal.WaitAsync(version).WaitAsync(HangGuard).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"任务 {id} 在 {HangGuard.TotalSeconds} 秒内没有状态变化，当前 {snapshot.State}、在跑执行 {snapshot.LiveExecutions}。");
        }
    }

    private void OnChanged(TaskId id) => _signal.Raise();

    public void Dispose() => _changes.Changed -= OnChanged;
}