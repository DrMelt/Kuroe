using ErrorOr;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Flow.Runtime;

/// <summary>按并发额度推进执行。派生只入队，收口回调在额度释放后于线程池上执行，可安全再派生。
/// 额度改动对后续入队即时生效。</summary>
public sealed class ExecutionDispatcher(IExecutionRunner runner, IExecutionPolicy policy)
{
    private readonly Lock _gate = new();
    private readonly Queue<Execution> _queue = [];
    private readonly Dictionary<ExecutionId, (Execution Execution, Task Task)> _live = [];
    private readonly Dictionary<ExecutionId, Action<Execution>> _settled = [];
    private bool _stopping;

    /// <summary>入队一个执行并等它收口。推进循环用，结果由任务侧按收口处理。</summary>
    public Task DispatchAsync(Execution execution, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(execution, _ => completion.TrySetResult());

        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>入队一个执行，跑完或失败后回调 settled。</summary>
    public void Dispatch(Execution execution, Action<Execution> settled)
    {
        List<Execution> dropped = [];
        lock (_gate)
        {
            _settled[execution.Id] = settled;
            if (_stopping)
            {
                execution.MarkCanceled();
                dropped.Add(execution);
            }
            else
            {
                _queue.Enqueue(execution);
                Pump(dropped);
            }
        }

        Settle(dropped);
    }

    /// <summary>取消排队与在跑的执行并等待收口，之后不再开新的。</summary>
    public async Task ShutdownAsync()
    {
        List<(Execution Execution, Task Task)> live;
        List<Execution> dropped;
        lock (_gate)
        {
            _stopping = true;
            dropped = [.. _queue];
            _queue.Clear();
            live = [.. _live.Values];
        }

        foreach (Execution execution in dropped)
        {
            execution.MarkCanceled();
        }

        Settle(dropped);

        foreach ((Execution execution, Task _) in live)
        {
            execution.Cancel();
        }

        await Task.WhenAll(live.Select(entry => entry.Task));
    }

    /// <summary>要求持有 <see cref="_gate"/>。额度有空位就取一个开跑，已取消的直接交回收口。</summary>
    private void Pump(List<Execution> dropped)
    {
        int limit = Math.Max(1, policy.MaxConcurrentExecutions);
        while (_queue.Count > 0 && _live.Count < limit)
        {
            Execution execution = _queue.Dequeue();
            if (execution.CancellationToken.IsCancellationRequested)
            {
                execution.MarkCanceled();
                dropped.Add(execution);
                continue;
            }

            _live[execution.Id] = (execution, Task.Run(() => ExecuteAsync(execution)));
        }
    }

    private async Task ExecuteAsync(Execution execution)
    {
        try
        {
            IExecutionBody body = runner.CreateBody(execution.Input);
            execution.AttachBody(body);
            execution.MarkRunning();
            ErrorOr<Success> outcome = await runner.ExecuteAsync(execution, body, execution.CancellationToken);
            if (outcome.IsError)
            {
                execution.MarkFailed(outcome.ErrorsOrEmptyList);
            }
            else
            {
                execution.MarkSucceeded();
            }
        }
        catch (OperationCanceledException)
        {
            execution.MarkCanceled();
        }
        catch (Exception ex)
        {
            execution.MarkFailed([Error.Failure("Execution.Failure", $"执行失败：{ex.Message}")]);
        }

        List<Execution> dropped = [];
        lock (_gate)
        {
            _live.Remove(execution.Id);
            Pump(dropped);
        }

        Settle([execution, .. dropped]);
    }

    /// <summary>取回并执行收口回调。必须在锁外调用，回调里会再派生。</summary>
    private void Settle(IReadOnlyList<Execution> executions)
    {
        foreach (Execution execution in executions)
        {
            Action<Execution>? settled;
            lock (_gate)
            {
                settled = _settled.GetValueOrDefault(execution.Id);
                _settled.Remove(execution.Id);
            }

            settled?.Invoke(execution);
        }
    }
}
