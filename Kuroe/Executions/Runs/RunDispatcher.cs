using ErrorOr;
using Kuroe.Configuration;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;

namespace Kuroe.Executions.Runs;

/// <summary>按并发额度推进 run。派生只入队，收口回调在额度释放后于线程池上执行，可安全再派生。
/// 额度改动对后续入队即时生效。</summary>
public sealed class RunDispatcher(IRunExecutor executor, SettingsProvider settings)
{
    private readonly Lock _gate = new();
    private readonly Queue<Run> _queue = [];
    private readonly Dictionary<RunId, (Run Run, Task Task)> _live = [];
    private readonly Dictionary<RunId, Action<Run>> _settled = [];
    private bool _stopping;

    /// <summary>入队一个 run 并等待收口，返回完整回复或错误。编排层用，行为与 <see cref="Dispatch"/> 一致。</summary>
    public Task<ErrorOr<string>> DispatchAsync(Run run, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<ErrorOr<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatch(run, settled =>
        {
            if (settled.State == RunState.Succeeded && settled.Result is { Length: > 0 })
            {
                completion.TrySetResult(settled.Result);
                return;
            }

            string description;
            if (settled.State == RunState.Canceled)
            {
                description = "已取消。";
            }
            else if (settled.Snapshot().Failures is { Count: > 0 } failures)
            {
                description = string.Join("；", failures);
            }
            else
            {
                description = "请求未正常结束。";
            }

            completion.TrySetResult(Error.Failure("Run.Dispatch", description));
        });

        return completion.Task.WaitAsync(cancellationToken);
    }

    /// <summary>入队一个 run，跑完或失败后回调 settled。</summary>
    public void Dispatch(Run run, Action<Run> settled)
    {
        List<Run> dropped = [];
        lock (_gate)
        {
            _settled[run.Id] = settled;
            if (_stopping)
            {
                run.MarkCanceled();
                dropped.Add(run);
            }
            else
            {
                _queue.Enqueue(run);
                Pump(dropped);
            }
        }

        Settle(dropped);
    }

    /// <summary>取消排队与在跑的 run 并等待收口，之后不再开新的。</summary>
    public async Task ShutdownAsync()
    {
        List<(Run Run, Task Task)> live;
        List<Run> dropped;
        lock (_gate)
        {
            _stopping = true;
            dropped = [.. _queue];
            _queue.Clear();
            live = [.. _live.Values];
        }

        foreach (Run run in dropped)
        {
            run.MarkCanceled();
        }

        Settle(dropped);

        foreach ((Run run, Task _) in live)
        {
            run.Cancel();
        }

        await Task.WhenAll(live.Select(entry => entry.Task));
    }

    /// <summary>要求持有 <see cref="_gate"/>。额度有空位就取一个开跑，已取消的直接交回收口。</summary>
    private void Pump(List<Run> dropped)
    {
        int limit = Math.Max(1, settings.Current.Runtime.MaxConcurrentRuns);
        while (_queue.Count > 0 && _live.Count < limit)
        {
            Run run = _queue.Dequeue();
            if (run.CancellationToken.IsCancellationRequested)
            {
                run.MarkCanceled();
                dropped.Add(run);
                continue;
            }

            _live[run.Id] = (run, Task.Run(() => ExecuteAsync(run)));
        }
    }

    private async Task ExecuteAsync(Run run)
    {
        try
        {
            run.MarkRunning();
            ErrorOr<string> reply = await executor.ExecuteAsync(run, run.CancellationToken);
            if (reply.IsError)
            {
                run.MarkFailed(reply.ErrorsOrEmptyList);
            }
            else
            {
                run.MarkSucceeded(reply.Value);
            }
        }
        catch (OperationCanceledException)
        {
            run.MarkCanceled();
        }
        catch (Exception ex)
        {
            run.MarkFailed([Error.Failure("Run.Failure", $"执行失败：{ex.Message}")]);
        }

        List<Run> dropped = [];
        lock (_gate)
        {
            _live.Remove(run.Id);
            Pump(dropped);
        }

        Settle([run, .. dropped]);
    }

    /// <summary>取回并执行收口回调。必须在锁外调用，回调里会再派生。</summary>
    private void Settle(IReadOnlyList<Run> runs)
    {
        foreach (Run run in runs)
        {
            Action<Run>? settled;
            lock (_gate)
            {
                settled = _settled[run.Id];
                _settled.Remove(run.Id);
            }

            settled(run);
        }
    }
}
