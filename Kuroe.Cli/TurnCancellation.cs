using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Workflows.TaskExecution.Tasks;

namespace Kuroe.Cli;

/// <summary>Ctrl+C 的取消范围：先当前对话回合，其次唯一的在跑 run，多个时只给出提示。</summary>
internal sealed class TurnCancellation(TaskRegistry registry, Terminal terminal) : IDisposable
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

        IReadOnlyList<RunSnapshot> live = registry.LiveRuns();
        switch (live.Count)
        {
            case 1:
                registry.FindRun(live[0].Id).Value.Cancel();
                terminal.Notice($"已取消 {live[0].Id}。", Styles.Warning);
                break;

            case > 1:
                terminal.Notice($"有 {live.Count} 个 run 在跑：{string.Join("、", live.Select(run => run.Id.Value))}，用 /task stop run <号> 指定。", Styles.Warning);
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
