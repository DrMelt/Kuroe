using ErrorOr;
using Kuroe.Kernel.Tasks.Persistence.Restoring.Shared;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>内置助手会话集合的管理层宿主：对外给出会话目录、轮次、历史与任务域生命周期。
/// 每个会话是一条独立任务，模型与语言在建立时固定，模式按会话切换。</summary>
public sealed class DialogueHost : IDisposable, IAsyncDisposable
{
    private readonly TaskActions _actions;
    private readonly DialogueSessions _sessions;
    private readonly DialogueTurns _turns;
    private readonly DialogueHistoryReader _history;
    private readonly ITaskRestoreService? _restore;
    private int _restored;
    private int _closed;

    internal DialogueHost(
        TaskActions actions,
        DialogueSessions sessions,
        DialogueTurns turns,
        DialogueHistoryReader history,
        ITaskRestoreService? restore)
    {
        _actions = actions;
        _sessions = sessions;
        _turns = turns;
        _history = history;
        _restore = restore;
    }

    /// <summary>一轮后台对话结束触发。</summary>
    public event Action<DialogueTurnNotice>? TurnCompleted
    {
        add => _turns.Completed += value;
        remove => _turns.Completed -= value;
    }

    /// <summary>当前会话摘要，没有当前会话时为空。</summary>
    public DialogueSessionInfo? Active => _sessions.Active;

    /// <summary>全部会话，按最近活动排序。</summary>
    public IReadOnlyList<DialogueSessionInfo> Sessions => _sessions.List();

    /// <summary>恢复会话存档并选择最近的会话。没有持久化时返回空报告，重复调用只返回空报告。</summary>
    public TaskLoadReport Restore()
    {
        if (Interlocked.CompareExchange(ref _restored, 1, 0) != 0)
        {
            return EmptyLoad();
        }

        try
        {
            TaskLoadReport report = _restore?.Restore() ?? EmptyLoad();
            _sessions.SelectLatest();

            return report;
        }
        catch
        {
            Interlocked.Exchange(ref _restored, 0);
            throw;
        }
    }

    /// <summary>新建会话。模型与语言取当前全局配置，标题与模式可由调用方给出。</summary>
    public ErrorOr<DialogueSessionInfo> Create(string? title = null, DialogueMode? mode = null) =>
        _sessions.Create(title, mode);

    /// <summary>选择当前会话。</summary>
    public ErrorOr<Success> Select(TaskId id) => _sessions.Select(id);

    /// <summary>切换指定会话的模式。下一轮执行按新模式建立工具面，不重建任务。</summary>
    public ErrorOr<Success> SelectMode(TaskId id, DialogueMode mode) => _sessions.SelectMode(id, mode);

    /// <summary>改会话标题。</summary>
    public ErrorOr<Success> Rename(TaskId id, string title) => _sessions.Rename(id, title);

    /// <summary>向指定会话发送一轮输入。会话正在执行时拒绝，不排队。</summary>
    public async Task<ErrorOr<Success>> SendAsync(
        TaskId id,
        string input,
        ProcessObserver? observer = null)
    {
        ErrorOr<WorkTask> found = _sessions.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        if (_turns.IsBusy(id))
        {
            return [DialogueErrors.Busy(id)];
        }

        _sessions.NameFromInput(found.Value, input);

        return await _turns.SendAsync(found.Value, input, observer).ConfigureAwait(false);
    }

    /// <summary>删除会话。运行中的任务先停止，等执行收口后移除任务、存档与租约。</summary>
    public Task<ErrorOr<Success>> DeleteAsync(TaskId id, CancellationToken cancellationToken = default) =>
        _sessions.DeleteAsync(id, cancellationToken);

    /// <summary>读取会话历史。基础历史取最后一次可读的恢复数据，其后的执行作为尾部返回。</summary>
    public Task<ErrorOr<DialogueHistory>> ReadHistoryAsync(
        TaskId id,
        CancellationToken cancellationToken = default) =>
        _history.ReadAsync(id, cancellationToken);

    /// <summary>写出会话存档、释放租约并丢弃本域。不中断流程，共享流程运行期由进程级关闭先收口。</summary>
    public async Task CloseAsync()
    {
        if (Interlocked.CompareExchange(ref _closed, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await _actions.CloseAsync().ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _closed, 0);
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose() => CloseAsync().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await CloseAsync().ConfigureAwait(false);

    private static TaskLoadReport EmptyLoad() => new([], [], []);
}