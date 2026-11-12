using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>按并发额度推进执行。派生只入队，收口回调在额度释放后于线程池上执行，可安全再派生。</summary>
public sealed class ExecutionDispatcher
{
    /// <summary>同时在跑的执行数上限，超出的排队等待。</summary>
    private const int MaxConcurrentExecutions = 4;

    private readonly Lock _gate = new();
    private readonly Queue<(Execution Execution, IExecutionRunner Runner)> _queue = [];
    private readonly Dictionary<ExecutionId, (Execution Execution, Task Task)> _live = [];
    private readonly Dictionary<ExecutionId, Action<Execution>> _settled = [];
    private bool _stopping;

    /// <summary>入队一个执行并等它收口。推进循环用，结果由任务侧按收口处理。</summary>
    public Task DispatchAsync(
        Execution execution,
        IExecutionRunner runner,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(execution, runner, _ => completion.TrySetResult());

        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>入队一个执行，跑完或失败后回调 settled。</summary>
    public void Dispatch(Execution execution, IExecutionRunner runner, Action<Execution> settled)
    {
        List<Execution> dropped = [];
        lock (_gate)
        {
            _settled[execution.Id] = settled;
            if (_stopping)
            {
                execution.MarkInterrupted("进程退出。");
                dropped.Add(execution);
            }
            else
            {
                _queue.Enqueue((execution, runner));
                Pump(dropped);
            }
        }

        Settle(dropped);
    }

    /// <summary>中断排队与在跑的执行并等待收口，之后不再开新的。</summary>
    public async Task ShutdownAsync()
    {
        List<(Execution Execution, Task Task)> live;
        List<Execution> dropped;
        lock (_gate)
        {
            _stopping = true;
            dropped = [.. _queue.Select(entry => entry.Execution)];
            _queue.Clear();
            live = [.. _live.Values];
        }

        foreach (Execution execution in dropped)
        {
            execution.MarkInterrupted("进程退出。");
        }

        Settle(dropped);

        foreach ((Execution execution, Task _) in live)
        {
            execution.Interrupt("进程退出。");
        }

        await Task.WhenAll(live.Select(entry => entry.Task));
    }

    /// <summary>要求持有 <see cref="_gate"/>。额度有空位就取一个开跑，已中断的直接交回收口。</summary>
    private void Pump(List<Execution> dropped)
    {
        while (_queue.Count > 0 && _live.Count < MaxConcurrentExecutions)
        {
            (Execution execution, IExecutionRunner runner) = _queue.Dequeue();
            if (execution.CancellationToken.IsCancellationRequested)
            {
                execution.MarkInterrupted();
                dropped.Add(execution);
                continue;
            }

            _live[execution.Id] = (execution, Task.Run(() => ExecuteAsync(execution, runner)));
        }
    }

    private async Task ExecuteAsync(Execution execution, IExecutionRunner runner)
    {
        try
        {
            execution.AttachJournal(runner.Journal);
            if (runner is IExecutionUsageSource usageReader)
            {
                execution.AttachUsageReader(usageReader);
            }

            if (runner is IExecutionRestoreDataSource restoreDataReader)
            {
                execution.AttachRestoreDataReader(restoreDataReader);
            }

            execution.MarkRunning();
            ErrorOr<ExecutionOutput> outcome = await runner.ExecuteAsync(
                execution.Request,
                execution.CancellationToken);
            if (outcome.IsError)
            {
                execution.MarkFailed(outcome.ErrorsOrEmptyList);
            }
            else
            {
                execution.MarkSucceeded(outcome.Value);
            }
        }
        catch (OperationCanceledException) when (execution.CancellationToken.IsCancellationRequested)
        {
            execution.MarkInterrupted();
        }
        catch (OperationCanceledException)
        {
            execution.MarkFailed([Error.Failure("Execution.Timeout", "模型请求超时，执行未收口。")]);
        }
        catch (Exception ex)
        {
            execution.MarkFailed([Error.Failure("Execution.Failure", $"执行失败：{ex.Message}")]);
        }
        finally
        {
            if (runner is IExecutionRestoreDataSource restoreDataReader)
            {
                execution.SettleRestoreData(restoreDataReader);
            }

            if (runner is IExecutionUsageSource reader)
            {
                execution.SettleUsage(reader.Usage);
            }
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