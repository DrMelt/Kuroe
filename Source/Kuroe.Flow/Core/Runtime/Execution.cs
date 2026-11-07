using ErrorOr;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;

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
    private ExecutionOutput? _output;
    private ExecutionUsage _usage = ExecutionUsage.None;
    private IExecutionUsageReader? _usageReader;
    private IExecutionRestoreStateReader? _restoreStateReader;
    private ExecutionRestoreState? _restoreState;
    private ExecutionContract _contract = ExecutionContract.Empty;
    private ExecutionId? _continuedFrom;
    private bool _interruptRequested;
    private string _interruptReason = "已中断。";
    private bool _startedNotified;

    internal Execution(ExecutionId id, ExecutionInput input)
        : this(id, input, 0)
    {
    }

    internal Execution(ExecutionId id, ExecutionInput input, int controlVersion)
    {
        Id = id;
        Input = input;
        ControlVersion = controlVersion;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private Execution(ExecutionId id, ExecutionInput input, DateTimeOffset createdAt)
    {
        Id = id;
        Input = input;
        CreatedAt = createdAt;
    }

    /// <summary>从存档恢复一次已收口的执行。未收口的执行记为已中断。</summary>
    internal static Execution Restore(ExecutionRecord record, ExecutionInput input)
    {
        Execution execution = new(record.Id, input, record.CreatedAt);
        execution.RestoreFromRecord(record);

        return execution;
    }

    /// <summary>恢复状态与历史，未收口的执行记为已中断。</summary>
    private void RestoreFromRecord(ExecutionRecord record)
    {
        bool live = record.State.IsLive();
        _state = live ? ExecutionState.Interrupted : record.State;
        _progress = record.Progress;
        _startedAt = record.StartedAt;
        _finishedAt = live ? DateTimeOffset.UtcNow : record.FinishedAt;
        _failures.AddRange(record.Failures);
        if (live)
        {
            _failures.Add("进程退出前未收口。");
        }

        _usage = record.Usage;
        _restoreState = record.RestoreState;
        _contract = record.Contract is { } contract
            ? new ExecutionContract(contract.Ports, contract.CannotCompleteReason)
            : ExecutionContract.Empty;
        _continuedFrom = record.ContinuedFrom;
        _cancellation.Dispose();
    }

    /// <summary>执行标识。</summary>
    public ExecutionId Id { get; }

    /// <summary>本次执行的输入。</summary>
    public ExecutionInput Input { get; }

    /// <summary>执行创建时刻。</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>创建时的任务控制代次，用于收口时判断控制动作是否已经发生。</summary>
    internal int ControlVersion { get; }

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
    public event ProcessContentHandler? ContentEmitted
    {
        add => _process.Content += value;
        remove => _process.Content -= value;
    }

    /// <summary>一条需要即时呈现的过程记录。</summary>
    public event JournalEntryHandler? EntryEmitted
    {
        add => _process.Entry += value;
        remove => _process.Entry -= value;
    }

    internal void MarkRunning()
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
    }

    /// <summary>执行进入运行态后触发一次，任务侧据此记录一次执行尝试。</summary>
    internal event Action? Started;

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

    /// <summary>附加本次执行的用量来源，快照读取其当前累计。</summary>
    internal void AttachUsageReader(IExecutionUsageReader reader)
    {
        lock (_gate)
        {
            _usageReader = reader;
        }
    }

    /// <summary>记下本次执行的最终用量并解除用量来源，快照不再随运行器变化。</summary>
    internal void SettleUsage(ExecutionUsage usage)
    {
        lock (_gate)
        {
            _usage = usage;
            _usageReader = null;
        }
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

    /// <summary>本次执行留下的恢复状态，不可恢复时为空。</summary>
    internal ExecutionRestoreState? RestoreState
    {
        get
        {
            lock (_gate)
            {
                return _restoreState;
            }
        }
    }

    /// <summary>恢复状态变化后触发，存档捕获据此写盘。</summary>
    internal event Action? RestoreStateChanged;

    /// <summary>本次执行从哪次执行恢复，独立执行时为空。</summary>
    internal ExecutionId? ContinuedFrom
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
    internal ExecutionContract Contract
    {
        get
        {
            lock (_gate)
            {
                return _contract;
            }
        }
    }

    /// <summary>记下本次执行从哪次执行恢复，并继承来源执行的契约状态。</summary>
    internal void ContinueFrom(Execution source)
    {
        ExecutionContract contract = source.Contract;
        lock (_gate)
        {
            _continuedFrom = source.Id;
            _contract = contract;
        }
    }

    /// <summary>记下本次执行交回的端口值。</summary>
    internal void SubmitPorts(IReadOnlyDictionary<PortName, PortValue> ports)
    {
        lock (_gate)
        {
            _contract = _contract with { Ports = ports };
        }
    }

    /// <summary>记下本次执行上报的无法完成原因，同一执行重复上报覆盖。</summary>
    internal void ReportCannotComplete(string reason)
    {
        lock (_gate)
        {
            _contract = _contract with { CannotCompleteReason = reason };
        }
    }

    /// <summary>附加运行器的恢复状态读取面，运行中的更新随快照可见。</summary>
    internal void AttachRestoreStateReader(IExecutionRestoreStateReader reader)
    {
        lock (_gate)
        {
            _restoreStateReader = reader;
            _restoreState = reader.RestoreState;
        }

        reader.RestoreStateChanged += OnRestoreStateChanged;
    }

    /// <summary>记下运行器的最终恢复状态并解除读取面。</summary>
    internal void SettleRestoreState(IExecutionRestoreStateReader? reader)
    {
        reader?.RestoreStateChanged -= OnRestoreStateChanged;

        lock (_gate)
        {
            _restoreStateReader = null;
            _restoreState = _state is ExecutionState.Succeeded or ExecutionState.Canceled
                ? null
                : reader?.RestoreState ?? _restoreState;
        }
    }

    /// <summary>丢弃恢复状态。重新执行、上游产出变化与任务收口时调用。</summary>
    internal void ClearRestoreState()
    {
        IExecutionRestoreStateReader? reader;
        bool changed;
        lock (_gate)
        {
            changed = _restoreState is not null;
            _restoreState = null;
            reader = _restoreStateReader;
            _restoreStateReader = null;
        }

        reader?.RestoreStateChanged -= OnRestoreStateChanged;

        if (changed)
        {
            RestoreStateChanged?.Invoke();
        }
    }

    private void OnRestoreStateChanged()
    {
        ExecutionRestoreState? state;
        lock (_gate)
        {
            state = _restoreStateReader?.RestoreState;
            if (Equals(state, _restoreState))
            {
                return;
            }

            _restoreState = state;
        }

        RestoreStateChanged?.Invoke();
    }

    /// <summary>本次执行的产出，端口产出的执行交回端口后只给空内容。</summary>
    internal ExecutionOutput? Output
    {
        get
        {
            lock (_gate)
            {
                return _output;
            }
        }
    }

    internal void MarkSucceeded(ExecutionOutput output)
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
        ExecutionUsage usage;
        IExecutionUsageReader? usageReader;
        ExecutionRestoreState? restoreState;
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
            restoreState = _restoreState;
            continuedFrom = _continuedFrom;
        }

        return new ExecutionSnapshot(Id, Input, state, progress, CreatedAt, startedAt, finishedAt,
            failures, journal?.Entries ?? [], usageReader?.Usage ?? usage,
            restoreState is not null, continuedFrom);
    }

    /// <summary>捕获本次执行的状态与恢复状态。</summary>
    internal ExecutionRecord CaptureRecord()
    {
        ExecutionSnapshot snapshot = Snapshot();
        ExecutionContract contract = Contract;

        return new ExecutionRecord(
            snapshot.Id,
            snapshot.Input.Node,
            snapshot.Input.ExecutionCount,
            snapshot.State,
            snapshot.Progress,
            snapshot.CreatedAt,
            snapshot.StartedAt,
            snapshot.FinishedAt,
            snapshot.Failures,
            snapshot.Usage,
            [
                .. snapshot.Input.Inputs.Select(value => new InputValueRecord(
                    value.Port,
                    RecordSources.Write(value.Origin),
                    value.Text)),
            ],
            snapshot.Input.Texts,
            snapshot.HasRestoreState ? RestoreState : null,
            ContinuedFrom,
            contract is { Ports: null, CannotCompleteReason: null }
                ? null
                : new ExecutionContractRecord(contract.Ports, contract.CannotCompleteReason));
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
