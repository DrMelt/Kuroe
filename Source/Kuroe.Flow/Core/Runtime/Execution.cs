using ErrorOr;
using Kuroe.Flow.Runtime.Process;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Flow.Runtime;

/// <summary>一次执行。状态由调度侧改动，过程记录由节点侧执行体写入，宿主读快照。</summary>
public sealed class Execution : IExecution
{
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ProcessStream _process = new();
    private IExecutionBody? _body;
    private ExecutionState _state = ExecutionState.Queued;
    private string _progress = string.Empty;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _finishedAt;
    private readonly List<string> _failures = [];

    internal Execution(ExecutionId id, ExecutionInput input)
    {
        Id = id;
        Input = input;
        Sink = new ExecutionSink(this, _process);
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>执行标识。</summary>
    public ExecutionId Id { get; }

    /// <summary>本次执行的输入。</summary>
    public ExecutionInput Input { get; }

    /// <summary>过程播发出口：文本增量与工具调用经它送给观察者。</summary>
    public IProcessSink Sink { get; }

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

    /// <summary>附加过程观察者，附加后收到本次执行的文本增量与工具调用记录。收口后附加不再有回调。</summary>
    public void Observe(IProcessSink observer) => _process.Observe(observer);

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

    /// <summary>记下本次执行的节点侧执行体，过程记录随之可见。</summary>
    internal void AttachBody(IExecutionBody body)
    {
        lock (_gate)
        {
            _body = body;
        }
    }

    internal void Report(string progress)
    {
        lock (_gate)
        {
            _progress = progress;
        }
    }

    internal void MarkSucceeded() => Settle(ExecutionState.Succeeded, []);

    internal void MarkFailed(IEnumerable<Error> errors) =>
        Settle(ExecutionState.Failed, [.. errors.Select(error => error.Description)]);

    internal void MarkCanceled() => Settle(ExecutionState.Canceled, ["已取消。"]);

    /// <summary>进入终态并记下失败原因，只认第一次。</summary>
    private void Settle(ExecutionState state, string[] failures)
    {
        lock (_gate)
        {
            if (_state.IsSettled())
            {
                return;
            }

            _state = state;
            _failures.AddRange(failures);
            _finishedAt = DateTimeOffset.UtcNow;
        }

        _cancellation.Dispose();
        _process.Close();
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
        lock (_gate)
        {
            return new ExecutionSnapshot(Id, Input, _state, _progress, CreatedAt, _startedAt, _finishedAt,
                [.. _failures], _body?.Journal.Entries ?? [], _body?.Journal.DroppedEntries ?? 0);
        }
    }

    /// <summary>执行侧的过程去处：把文本增量与工具调用播发给观察者，并上报进度。</summary>
    private sealed class ExecutionSink(Execution execution, ProcessStream process) : IProcessSink
    {
        public void OnText(string delta) => process.OnText(delta);

        public void OnToolCall(ToolCallRecord record)
        {
            execution.Report($"调用工具 {record.Name}");
            process.OnToolCall(record);
        }
    }
}
