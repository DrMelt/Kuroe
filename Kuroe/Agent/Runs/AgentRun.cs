using ErrorOr;
using Kuroe.Agent.Turns;

namespace Kuroe.Agent.Runs;

/// <summary>一次 agent 执行。状态与记录只由调度侧改动，宿主读快照。</summary>
public sealed class AgentRun
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private RunState _state = RunState.Queued;
    private string _progress = string.Empty;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private string? _result;
    private readonly List<string> _failures = [];

    internal AgentRun(RunId id, RunContext context)
    {
        Id = id;
        Context = context;
        Journal = new TurnJournal();
        CreatedAt = DateTimeOffset.UtcNow;
        Scope = new TurnScope
        {
            Task = context.Task,
            Run = id,
            Role = context.Role,
            StepName = context.StepName,
            ItemIndex = context.ItemIndex,
            Journal = Journal,
            Sink = ITurnSink.For(Journal, new ProgressSink(this)),
        };
    }

    /// <summary>agent 标识。</summary>
    public RunId Id { get; }

    /// <summary>执行上下文。</summary>
    public RunContext Context { get; }

    /// <summary>过程记录。</summary>
    public TurnJournal Journal { get; }

    /// <summary>该 agent 的回合归属，工具与提交通道用它认定身份。</summary>
    public TurnScope Scope { get; }

    /// <summary>登记时刻。</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>当前执行状态。</summary>
    public RunState State
    {
        get
        {
            lock (_gate)
            {
                return _state;
            }
        }
    }

    /// <summary>收口时的完整回复，未收口时为空。</summary>
    public string? Result
    {
        get
        {
            lock (_gate)
            {
                return _result;
            }
        }
    }

    /// <summary>还在排队或正在执行。</summary>
    public bool IsLive => State.IsLive();

    internal CancellationToken CancellationToken => _cancellation.Token;

    internal void MarkRunning()
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = RunState.Running;
            _startedAt ??= DateTimeOffset.UtcNow;
        }
    }

    internal void Report(string progress)
    {
        lock (_gate)
        {
            _progress = progress;
        }
    }

    internal void MarkSucceeded(string result) => Settle(RunState.Succeeded, result, []);

    internal void MarkFailed(IEnumerable<Error> errors) =>
        Settle(RunState.Failed, null, [.. errors.Select(error => error.Description)]);

    internal void MarkCanceled() => Settle(RunState.Canceled, null, ["已取消。"]);

    /// <summary>进入终态并交出结果，只认第一次；失败原因同时写进过程记录。</summary>
    private void Settle(RunState state, string? result, string[] failures)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = state;
            _result = result;
            _failures.AddRange(failures);
            _finishedAt = DateTimeOffset.UtcNow;
        }

        foreach (string text in failures)
        {
            Journal.Append(new ErrorEntry(text));
        }
    }

    /// <summary>该 agent 的请求正常结束但没交回本步骤要求的东西，从已完成降级为失败。要求持有任务 Gate。</summary>
    internal void MarkUncollected(string reason)
    {
        lock (_gate)
        {
            if (_state != RunState.Succeeded)
            {
                return;
            }

            _state = RunState.Failed;
            _result = null;
            _failures.Add(reason);
        }

        Journal.Append(new ErrorEntry(reason));
    }

    /// <summary>取消该 agent，进行中的一轮请求随之结束。</summary>
    public void Cancel() => _cancellation.Cancel();

    /// <summary>当前状态的只读快照。</summary>
    public RunSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new RunSnapshot(Id, Context, _state, _progress, CreatedAt, _startedAt, _finishedAt, _result,
                [.. _failures], Journal.Entries, Journal.DroppedEntries);
        }
    }

    /// <summary>把该 agent 的过程写成当前活动，只有进行中的工具调用值得标出。</summary>
    private sealed class ProgressSink(AgentRun run) : ITurnSink
    {
        public void OnText(string delta) => run.Report(string.Empty);

        public void OnToolCall(ToolCallRecord record) => run.Report($"调用工具 {record.Name}");
    }
}
