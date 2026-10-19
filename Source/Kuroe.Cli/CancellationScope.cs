using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Runtime.Tasks;

namespace Kuroe.Cli;

/// <summary>Ctrl+C 的取消范围：先当前一轮对话请求，其次唯一的在跑执行，多个时只给出提示。</summary>
internal sealed class CancellationScope(TaskRegistry registry, Terminal terminal) : IDisposable
{
    private readonly Lock _gate = new();
    private CancellationTokenSource? _current;

    /// <summary>开始一轮请求并返回该轮的取消令牌，上一轮的取消源随之释放。</summary>
    public CancellationToken Begin()
    {
        lock (_gate)
        {
            _current?.Dispose();
            _current = new CancellationTokenSource();

            return _current.Token;
        }
    }

    public void Cancel()
    {
        lock (_gate)
        {
            if (_current is { } source)
            {
                source.Cancel();
                return;
            }
        }

        IReadOnlyList<ExecutionSnapshot> live = registry.LiveExecutions();
        switch (live.Count)
        {
            case 1:
                registry.FindExecution(live[0].Id).Value.Cancel();
                terminal.Notice($"已取消 {live[0].Id}。", Styles.Warning);
                break;

            case > 1:
                terminal.Notice($"有 {live.Count} 个执行在跑：{string.Join("、", live.Select(execution => execution.Id.Value))}，用 /task stop execution <号> 指定。", Styles.Warning);
                break;

            default:
                terminal.Notice("没有可中断的执行。", Styles.Hint);
                break;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _current?.Dispose();
            _current = null;
        }
    }
}
