using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Execution;

/// <summary>按并发额度推进执行。只负责排队与调度，运行器接线与收口由 ExecutionRun 完成；收口回调在额度释放后于线程池上执行，可安全再派生。</summary>
public sealed class ExecutionDispatcher
{
    /// <summary>同时在跑的执行数上限，超出的排队等待。</summary>
    private const int MaxConcurrentExecutions = 4;

    private readonly Lock _gate = new();
    private readonly Queue<ExecutionRun> _queue = [];
    private readonly Dictionary<ExecutionId, (ExecutionInstance Instance, Task Task)> _live = [];
    private readonly Dictionary<ExecutionId, Action<IExecution>> _settled = [];
    private bool _stopping;

    /// <summary>入队一个执行并等它收口。推进循环用，结果由任务侧按收口处理。</summary>
    internal Task DispatchAsync(
        ExecutionRun run,
        CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(run, _ => completion.TrySetResult());

        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>入队一个执行，跑完或失败后回调 settled。</summary>
    internal void Dispatch(ExecutionRun run, Action<IExecution> settled)
    {
        ExecutionInstance unit = run.Execution;
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
                _queue.Enqueue(run);
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
            dropped = [.. _queue.Select(run => run.Execution)];
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
            ExecutionRun run = _queue.Dequeue();
            ExecutionInstance instance = run.Execution;
            if (instance.CancellationToken.IsCancellationRequested)
            {
                instance.MarkInterrupted();
                dropped.Add(instance);
                continue;
            }

            _live[instance.Id] = (instance, Task.Run(() => ExecuteAsync(run)));
        }
    }

    private async Task ExecuteAsync(ExecutionRun run)
    {
        try
        {
            await run.ExecuteAsync().ConfigureAwait(false);
        }
        finally
        {
            ExecutionInstance execution = run.Execution;
            List<ExecutionInstance> dropped = [];
            lock (_gate)
            {
                _live.Remove(execution.Id);
                Pump(dropped);
            }

            Settle([execution, .. dropped]);
        }
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
}
