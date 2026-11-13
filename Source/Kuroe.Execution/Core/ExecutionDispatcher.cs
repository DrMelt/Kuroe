using ErrorOr;
using Kuroe.Execution.Shared;

namespace Kuroe.Execution;

/// <summary>按并发额度推进执行。派生只入队，收口回调在额度释放后于线程池上执行，可安全再派生。</summary>
public sealed class ExecutionDispatcher
{
    /// <summary>同时在跑的执行数上限，超出的排队等待。</summary>
    private const int MaxConcurrentExecutions = 4;

    private readonly Lock _gate = new();
    private readonly Queue<(ExecutionInstance Instance, IExecutionRunner Runner)> _queue = [];
    private readonly Dictionary<ExecutionId, (ExecutionInstance Instance, Task Task)> _live = [];
    private readonly Dictionary<ExecutionId, Action<IExecution>> _settled = [];
    private bool _stopping;

    /// <summary>入队一个执行并等它收口。推进循环用，结果由任务侧按收口处理。</summary>
    public Task DispatchAsync(
        IExecution execution,
        IExecutionRunner runner,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(Unit(execution), runner, _ => completion.TrySetResult());

        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>入队一个执行，跑完或失败后回调 settled。</summary>
    public void Dispatch(IExecution execution, IExecutionRunner runner, Action<IExecution> settled)
    {
        ExecutionInstance unit = Unit(execution);
        List<ExecutionInstance> dropped = [];
        lock (_gate)
        {
            _settled[unit.Id] = settled;
            if (_stopping)
            {
                unit.MarkInterrupted("进程退出。");
                dropped.Add(unit);
            }
            else
            {
                _queue.Enqueue((unit, runner));
                Pump(dropped);
            }
        }

        Settle(dropped);
    }

    /// <summary>中断排队与在跑的执行并等待收口，之后不再开新的。</summary>
    public async Task ShutdownAsync()
    {
        List<(ExecutionInstance Instance, Task Task)> live;
        List<ExecutionInstance> dropped;
        lock (_gate)
        {
            _stopping = true;
            dropped = [.. _queue.Select(entry => entry.Instance)];
            _queue.Clear();
            live = [.. _live.Values];
        }

        foreach (ExecutionInstance instance in dropped)
        {
            instance.MarkInterrupted("进程退出。");
        }

        Settle(dropped);

        foreach ((ExecutionInstance instance, Task _) in live)
        {
            instance.Interrupt("进程退出。");
        }

        await Task.WhenAll(live.Select(entry => entry.Task));
    }

    /// <summary>要求持有 <see cref="_gate"/>。额度有空位就取一个开跑，已中断的直接交回收口。</summary>
    private void Pump(List<ExecutionInstance> dropped)
    {
        while (_queue.Count > 0 && _live.Count < MaxConcurrentExecutions)
        {
            (ExecutionInstance instance, IExecutionRunner runner) = _queue.Dequeue();
            if (instance.CancellationToken.IsCancellationRequested)
            {
                instance.MarkInterrupted();
                dropped.Add(instance);
                continue;
            }

            _live[instance.Id] = (instance, Task.Run(() => ExecuteAsync(instance, runner)));
        }
    }

    private async Task ExecuteAsync(ExecutionInstance execution, IExecutionRunner runner)
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

        List<ExecutionInstance> dropped = [];
        lock (_gate)
        {
            _live.Remove(execution.Id);
            Pump(dropped);
        }

        Settle([execution, .. dropped]);
    }

    /// <summary>取回并执行收口回调。必须在锁外调用，回调里会再派生。</summary>
    private void Settle(IReadOnlyList<ExecutionInstance> executions)
    {
        foreach (ExecutionInstance execution in executions)
        {
            Action<IExecution>? settled;
            lock (_gate)
            {
                settled = _settled.GetValueOrDefault(execution.Id);
                _settled.Remove(execution.Id);
            }

            settled?.Invoke(execution);
        }
    }

    private static ExecutionInstance Unit(IExecution execution) => (ExecutionInstance)execution;
}