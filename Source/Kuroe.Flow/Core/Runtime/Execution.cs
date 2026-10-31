using ErrorOr;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime;

/// <summary>一次执行。状态由调度侧改动，过程记录来自附加的记录，宿主读快照与过程事件。</summary>
public sealed class Execution : IExecution
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ProcessBroadcast _process = new();
    private IProcessJournal? _journal;
    private ExecutionState _state = ExecutionState.Queued;
    private string _progress = string.Empty;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private readonly List<string> _failures = [];
    private bool _interruptRequested;
    private string _interruptReason = "已中断。";

    internal Execution(ExecutionId id, ExecutionInput input)
    {
        Id = id;
        Input = input;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private Execution(ExecutionId id, ExecutionInput input, DateTimeOffset createdAt)
    {
        Id = id;
        Input = input;
        CreatedAt = createdAt;
    }

    /// <summary>从检查点恢复一次已收口的执行。未收口的执行记为已中断。</summary>
    internal static Execution Restore(ExecutionCheckpoint checkpoint, ExecutionInput input)
    {
        Execution execution = new(checkpoint.Id, input, checkpoint.CreatedAt);
        execution.RestoreState(checkpoint);

        return execution;
    }

    /// <summary>恢复状态与历史，未收口的执行记为已中断。</summary>
    private void RestoreState(ExecutionCheckpoint checkpoint)
    {
        bool live = checkpoint.State.IsLive();
        _state = live ? ExecutionState.Interrupted : checkpoint.State;
        _progress = checkpoint.Progress;
        _startedAt = checkpoint.StartedAt;
        _finishedAt = live ? DateTimeOffset.UtcNow : checkpoint.FinishedAt;
        _failures.AddRange(checkpoint.Failures);
        if (live)
        {
            _failures.Add("进程退出前未收口。");
        }

        _journal = new RestoredProcessJournal(checkpoint.Journal, checkpoint.DroppedEntries);
        _cancellation.Dispose();
    }

    /// <summary>执行标识。</summary>
    public ExecutionId Id { get; }

    /// <summary>本次执行的输入。</summary>
    public ExecutionInput Input { get; }

    /// <summary>执行创建时刻。</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>当前执行状态。</summary>
    public ExecutionState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>还在排队或正在执行。</summary>
    public bool IsLive => State.IsLive();

    internal CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>模型输出的一段流式内容，按到达顺序给出。</summary>
    public event Action<ProcessContent>? ContentEmitted
    {
        add => _process.Content += value;
        remove => _process.Content -= value;
    }

    /// <summary>一条需要即时呈现的过程记录。</summary>
    public event Action<JournalEntry>? EntryEmitted
    {
        add => _process.Entry += value;
        remove => _process.Entry -= value;
    }

    internal void MarkRunning()
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = ExecutionState.Running;
            _startedAt ??= DateTimeOffset.UtcNow;
        }
    }

    /// <summary>记下本次执行的过程记录，快照随之可见，过程通知转给订阅者。</summary>
    internal void AttachJournal(IProcessJournal journal)
    {
        lock (_gate)
        {
            _journal = journal;
        }

        journal.ContentAppended += PublishContent;
        journal.EntryAppended += PublishEntry;
        journal.ProgressReported += UpdateProgress;
    }

    /// <summary>转给订阅者一段流式内容。</summary>
    private void PublishContent(ProcessContent content)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }
        }

        _process.PublishContent(content);
    }

    /// <summary>更新进度。</summary>
    private void UpdateProgress(string text)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _progress = text;
        }
    }

    /// <summary>转给订阅者一条需要即时呈现的过程记录。</summary>
    private void PublishEntry(JournalEntry entry)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }
        }

        _process.PublishEntry(entry);
    }

    internal void MarkSucceeded() => Settle(ExecutionState.Succeeded, []);

    internal void MarkFailed(IEnumerable<Error> errors) =>
        Settle(ExecutionState.Failed, [.. errors.Select(error => error.Description)]);

    internal void MarkCanceled() => Settle(ExecutionState.Canceled, ["已取消。"]);

    /// <summary>请求中断本次执行，进行中的一轮请求随之结束。要求持有任务 Gate。</summary>
    internal void Interrupt(string reason = "已中断。")
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _interruptRequested = true;
            _interruptReason = reason;
            _cancellation.Cancel();
        }
    }

    /// <summary>按中断请求收口：请求过中断记为已中断，否则记为已取消。</summary>
    internal void MarkCanceledOrInterrupted()
    {
        ExecutionState state;
        string reason;
        lock (_gate)
        {
            state = _interruptRequested ? ExecutionState.Interrupted : ExecutionState.Canceled;
            reason = _interruptRequested ? _interruptReason : "已取消。";
        }

        Settle(state, [reason]);
    }

    /// <summary>直接记为已中断，用于未进入执行的排队项与退出时的丢弃项。</summary>
    internal void MarkInterrupted(string reason = "已中断。")
    {
        lock (_gate)
        {
            _interruptRequested = true;
            _interruptReason = reason;
        }

        Settle(ExecutionState.Interrupted, [reason]);
    }

    /// <summary>进入终态并记下失败原因，只认第一次。</summary>
    private void Settle(ExecutionState state, string[] failures)
    {
        IProcessJournal? journal;
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = state;
            _failures.AddRange(failures);
            _finishedAt = DateTimeOffset.UtcNow;
            journal = _journal;
        }

        _process.Close();

        if (journal is not null)
        {
            journal.ContentAppended -= PublishContent;
            journal.EntryAppended -= PublishEntry;
            journal.ProgressReported -= UpdateProgress;
        }

        _cancellation.Dispose();
    }

    /// <summary>本次请求正常结束但没交回本执行节点要求的东西，从已完成降级为失败。要求持有任务 Gate。</summary>
    internal void MarkUncollected(string reason)
    {
        lock (_gate)
        {
            if (_state != ExecutionState.Succeeded)
            {
                return;
            }

            _state = ExecutionState.Failed;
            _failures.Add(reason);
        }
    }

    /// <summary>取消本次执行，进行中的一轮请求随之结束。</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _cancellation.Cancel();
        }
    }

    /// <summary>当前状态的只读快照。</summary>
    public ExecutionSnapshot Snapshot()
    {
        ExecutionState state;
        string progress;
        DateTimeOffset? startedAt;
        DateTimeOffset? finishedAt;
        string[] failures;
        IProcessJournal? journal;
        lock (_gate)
        {
            state = _state;
            progress = _progress;
            startedAt = _startedAt;
            finishedAt = _finishedAt;
            failures = [.. _failures];
            journal = _journal;
        }

        return new ExecutionSnapshot(Id, Input, state, progress, CreatedAt, startedAt, finishedAt,
            failures, journal?.Entries ?? [], journal?.DroppedEntries ?? 0);
    }

    /// <summary>捕获本次执行的状态与历史。</summary>
    internal ExecutionCheckpoint CaptureCheckpoint()
    {
        ExecutionSnapshot snapshot = Snapshot();

        return new ExecutionCheckpoint(
            snapshot.Id,
            snapshot.Input.Node,
            snapshot.Input.ExecutionCount,
            snapshot.State,
            snapshot.Progress,
            snapshot.CreatedAt,
            snapshot.StartedAt,
            snapshot.FinishedAt,
            snapshot.Failures,
            snapshot.Journal,
            snapshot.DroppedEntries,
            [
                .. snapshot.Input.Inputs.Select(value => new InputValueCheckpoint(
                    value.Port,
                    CheckpointSources.Write(value.Origin),
                    value.Text)),
            ],
            snapshot.Input.Texts);
    }

    /// <summary>过程事件的广播：收口后不再接受订阅，也不再转发。</summary>
    private sealed class ProcessBroadcast
    {
        private readonly Lock _gate = new();
        private Action<ProcessContent>? _content;
        private Action<JournalEntry>? _entry;
        private bool _closed;

        public event Action<ProcessContent>? Content
        {
            add
            {
                lock (_gate)
                {
                    if (!_closed)
                    {
                        _content += value;
                    }
                }
            }
            remove
            {
                lock (_gate)
                {
                    _content -= value;
                }
            }
        }

        public event Action<JournalEntry>? Entry
        {
            add
            {
                lock (_gate)
                {
                    if (!_closed)
                    {
                        _entry += value;
                    }
                }
            }
            remove
            {
                lock (_gate)
                {
                    _entry -= value;
                }
            }
        }

        public void PublishContent(ProcessContent content)
        {
            Action<ProcessContent>? handler;
            lock (_gate)
            {
                handler = _content;
            }

            handler?.Invoke(content);
        }

        public void PublishEntry(JournalEntry entry)
        {
            Action<JournalEntry>? handler;
            lock (_gate)
            {
                handler = _entry;
            }

            handler?.Invoke(entry);
        }

        /// <summary>清空订阅者并拒绝之后的订阅。</summary>
        public void Close()
        {
            lock (_gate)
            {
                _closed = true;
                _content = null;
                _entry = null;
            }
        }
    }
}
