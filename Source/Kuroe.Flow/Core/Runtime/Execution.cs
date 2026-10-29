using ErrorOr;
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

    internal Execution(ExecutionId id, ExecutionInput input)
    {
        Id = id;
        Input = input;
        CreatedAt = DateTimeOffset.UtcNow;
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

    /// <summary>模型输出的一段文本，按到达顺序给出。</summary>
    public event Action<string>? TextEmitted
    {
        add => _process.Text += value;
        remove => _process.Text -= value;
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

        journal.TextAppended += PublishText;
        journal.EntryAppended += PublishEntry;
        journal.ProgressReported += UpdateProgress;
    }

    /// <summary>转给订阅者一段文本增量。</summary>
    private void PublishText(string delta)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }
        }

        _process.PublishText(delta);
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
            journal.TextAppended -= PublishText;
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

    /// <summary>过程事件的广播：收口后不再接受订阅，也不再转发。</summary>
    private sealed class ProcessBroadcast
    {
        private readonly Lock _gate = new();
        private Action<string>? _text;
        private Action<JournalEntry>? _entry;
        private bool _closed;

        public event Action<string>? Text
        {
            add
            {
                lock (_gate)
                {
                    if (!_closed)
                    {
                        _text += value;
                    }
                }
            }
            remove
            {
                lock (_gate)
                {
                    _text -= value;
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

        public void PublishText(string delta)
        {
            Action<string>? handler;
            lock (_gate)
            {
                handler = _text;
            }

            handler?.Invoke(delta);
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
                _text = null;
                _entry = null;
            }
        }
    }
}
