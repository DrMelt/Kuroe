using ErrorOr;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Execution;

/// <summary>一次执行的状态机：排队、运行、收口、中断与过程记录。执行只使用执行请求与中性产出，不读取节点与图。</summary>
public sealed class Execution
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
    private ExecutionOutput? _output;
    private ExecutionUsage _usage = ExecutionUsage.None;
    private ExecutionRestoreState? _restoreData;
    private IExecutionUsageSource? _usageReader;
    private IExecutionRestoreDataSource? _restoreReader;
    private ExecutionContract _contract = ExecutionContract.Empty;
    private ExecutionId? _continuedFrom;
    private string _interruptReason = "已中断。";
    private bool _startedNotified;

    /// <summary>建立一次排队中的执行。</summary>
    public Execution(ExecutionId id, ExecutionRequest request)
        : this(id, request, DateTimeOffset.UtcNow)
    {
    }

    /// <summary>建立一次指定创建时刻的排队执行。</summary>
    public Execution(ExecutionId id, ExecutionRequest request, DateTimeOffset createdAt)
    {
        Id = id;
        Request = request;
        CreatedAt = createdAt;
    }

    /// <summary>执行标识。</summary>
    public ExecutionId Id { get; }

    /// <summary>本次执行的中性请求。</summary>
    public ExecutionRequest Request { get; }

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

    /// <summary>中断令牌。</summary>
    public CancellationToken CancellationToken => _cancellation.Token;

    /// <summary>快照可见的状态或过程变化。</summary>
    public event Action? Changed;

    /// <summary>执行进入运行态后触发一次。</summary>
    public event Action? Started;

    /// <summary>流式内容到达。</summary>
    public event ProcessContentHandler? ContentEmitted
    {
        add => _process.Content += value;
        remove => _process.Content -= value;
    }

    /// <summary>需要即时呈现的过程记录写入。</summary>
    public event JournalEntryHandler? EntryEmitted
    {
        add => _process.Entry += value;
        remove => _process.Entry -= value;
    }

    /// <summary>恢复数据变化。</summary>
    public event Action? RestoreDataChanged;

    /// <summary>进入运行态。</summary>
    public void MarkRunning()
    {
        bool started;
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = ExecutionState.Running;
            _startedAt ??= DateTimeOffset.UtcNow;
            started = !_startedNotified;
            _startedNotified = true;
        }

        if (started)
        {
            Started?.Invoke();
        }

        Changed?.Invoke();
    }

    /// <summary>附加本次执行的过程记录，过程通知转给订阅者。</summary>
    public void AttachJournal(IProcessJournal journal)
    {
        lock (_gate)
        {
            _journal = journal;
        }

        journal.ContentAppended += PublishContent;
        journal.EntryAppended += PublishEntry;
        journal.ProgressReported += UpdateProgress;
    }

    /// <summary>附加本次执行的用量来源，快照读取其当前累计。</summary>
    public void AttachUsageReader(IExecutionUsageSource reader)
    {
        lock (_gate)
        {
            _usageReader = reader;
        }
    }

    /// <summary>记下本次执行的最终用量并解除用量来源。</summary>
    public void SettleUsage(ExecutionUsage usage)
    {
        lock (_gate)
        {
            _usage = usage;
            _usageReader = null;
        }
    }

    /// <summary>本次执行保留的恢复数据。</summary>
    public ExecutionRestoreState? RestoreData
    {
        get
        {
            lock (_gate)
            {
                return _restoreData;
            }
        }
    }

    /// <summary>记下恢复数据，变化时通知。</summary>
    public void SetRestoreData(ExecutionRestoreState? data)
    {
        bool changed;
        lock (_gate)
        {
            changed = !Equals(data, _restoreData);
            _restoreData = data;
        }

        if (changed)
        {
            RestoreDataChanged?.Invoke();
        }
    }

    /// <summary>附加运行器的恢复数据读取面。</summary>
    public void AttachRestoreDataReader(IExecutionRestoreDataSource reader)
    {
        lock (_gate)
        {
            _restoreReader = reader;
        }

        SetRestoreData(reader.RestoreData);
        reader.RestoreDataChanged += OnRestoreDataChanged;
    }

    /// <summary>记下运行器的最终恢复数据并解除读取面。</summary>
    public void SettleRestoreData(IExecutionRestoreDataSource? reader)
    {
        reader?.RestoreDataChanged -= OnRestoreDataChanged;
        lock (_gate)
        {
            _restoreReader = null;
        }

        SetRestoreData(reader?.RestoreData);
    }

    private void OnRestoreDataChanged()
    {
        IExecutionRestoreDataSource? reader;
        lock (_gate)
        {
            reader = _restoreReader;
        }

        SetRestoreData(reader?.RestoreData);
    }

    /// <summary>本次执行从哪次执行恢复。</summary>
    public ExecutionId? ContinuedFrom
    {
        get
        {
            lock (_gate)
            {
                return _continuedFrom;
            }
        }
    }

    /// <summary>本次执行的本轮契约状态。</summary>
    public ExecutionContract Contract
    {
        get
        {
            lock (_gate)
            {
                return _contract;
            }
        }
    }

    /// <summary>本次执行的产出。</summary>
    public ExecutionOutput? Output
    {
        get
        {
            lock (_gate)
            {
                return _output;
            }
        }
    }

    /// <summary>记下从哪次执行恢复，并继承来源执行的契约状态。</summary>
    public void ContinueFrom(Execution source)
    {
        ExecutionContract contract = source.Contract;
        lock (_gate)
        {
            _continuedFrom = source.Id;
            _contract = contract;
        }
    }

    /// <summary>记下本次执行交回的端口值。</summary>
    public void SubmitOutputs(IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs)
    {
        lock (_gate)
        {
            _contract = _contract with { Outputs = outputs };
        }
    }

    /// <summary>记下本次执行上报的无法完成原因。</summary>
    public void ReportCannotComplete(string reason)
    {
        lock (_gate)
        {
            _contract = _contract with { CannotCompleteReason = reason };
        }
    }
/// <summary>进入成功终态并记下产出。</summary>
    public void MarkSucceeded(ExecutionOutput output)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _output = output;
        }

        Settle(ExecutionState.Succeeded, []);
    }

    /// <summary>进入失败终态并记下失败原因。</summary>
    public void MarkFailed(IEnumerable<Error> errors) =>
        Settle(ExecutionState.Failed, [.. errors.Select(error => error.Description)]);

    /// <summary>请求中断本次执行，进行中的一轮请求随之结束。</summary>
    public void Interrupt(string reason = "已中断。")
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _interruptReason = reason;
            _cancellation.Cancel();
        }
    }

    /// <summary>直接记为已中断，用于未进入执行的排队项与退出时的丢弃项。</summary>
    public void MarkInterrupted(string? reason = null)
    {
        string text;
        lock (_gate)
        {
            text = reason ?? _interruptReason;
            _interruptReason = text;
        }

        Settle(ExecutionState.Interrupted, [text]);
    }

    /// <summary>本次请求正常结束但没交回要求的内容，从已完成降级为失败。</summary>
    public void MarkUncollected(string reason)
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

    /// <summary>当前状态的只读快照。</summary>
    public ExecutionSnapshot Snapshot()
    {
        ExecutionState state;
        string progress;
        DateTimeOffset? startedAt;
        DateTimeOffset? finishedAt;
        string[] failures;
        IProcessJournal? journal;
        ExecutionUsage usage;
        IExecutionUsageSource? usageReader;
        ExecutionRestoreState? restoreData;
        ExecutionContract contract;
        ExecutionOutput? output;
        ExecutionId? continuedFrom;
        lock (_gate)
        {
            state = _state;
            progress = _progress;
            startedAt = _startedAt;
            finishedAt = _finishedAt;
            failures = [.. _failures];
            journal = _journal;
            usage = _usage;
            usageReader = _usageReader;
            restoreData = _restoreData;
            contract = _contract;
            output = _output;
            continuedFrom = _continuedFrom;
        }

        usage = usageReader?.Usage ?? usage;

        return new ExecutionSnapshot(
            Id, Request, state, progress, CreatedAt, startedAt, finishedAt, failures,
            journal?.Entries ?? [], usage, restoreData, contract, output, continuedFrom);
    }
/// <summary>按存档状态建立已收口的执行，未收口的执行记为已中断。</summary>
    public static Execution Restore(
        ExecutionId id,
        ExecutionRequest request,
        DateTimeOffset createdAt,
        ExecutionState state,
        string progress,
        DateTimeOffset? startedAt,
        DateTimeOffset? finishedAt,
        IReadOnlyList<string> failures,
        ExecutionUsage usage,
        ExecutionRestoreState? restoreData,
        ExecutionContract contract,
        ExecutionId? continuedFrom)
    {
        Execution unit = new(id, request, createdAt);
        bool live = state.IsLive();
        unit._state = live ? ExecutionState.Interrupted : state;
        unit._progress = progress;
        unit._startedAt = startedAt;
        unit._finishedAt = live ? DateTimeOffset.UtcNow : finishedAt;
        unit._failures.AddRange(failures);
        if (live)
        {
            unit._failures.Add("进程退出前未收口。");
        }

        unit._usage = usage;
        unit._restoreData = restoreData;
        unit._contract = contract;
        unit._continuedFrom = continuedFrom;
        unit._cancellation.Dispose();

        return unit;
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
            if (_state.IsSettled() || _progress == text)
            {
                return;
            }

            _progress = text;
        }

        Changed?.Invoke();
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
        Changed?.Invoke();
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
        Changed?.Invoke();
    }
/// <summary>过程事件的广播：收口后不再接受订阅，也不再转发。</summary>
    private sealed class ProcessBroadcast
    {
        private readonly Lock _gate = new();
        private ProcessContentHandler? _content;
        private JournalEntryHandler? _entry;
        private bool _closed;

        public event ProcessContentHandler? Content
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

        public event JournalEntryHandler? Entry
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
            ProcessContentHandler? handler;
            lock (_gate)
            {
                handler = _content;
            }

            handler?.Invoke(content);
        }

        public void PublishEntry(JournalEntry entry)
        {
            JournalEntryHandler? handler;
            lock (_gate)
            {
                handler = _entry;
            }

            handler?.Invoke(entry);
        }

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