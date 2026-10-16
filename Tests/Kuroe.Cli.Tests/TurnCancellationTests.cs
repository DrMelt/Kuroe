using Kuroe.Flow.Shared.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>Ctrl+C 的取消范围：先当前对话回合，其次唯一的在跑 run，多个时只给出提示。</summary>
public sealed class TurnCancellationTests
{
    private const int LongDelay = 20000;

    [Fact]
    public void Begin_hands_out_a_token_and_cancel_triggers_it()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        using TurnCancellation cancellation = NewCancellation(environment, out _);

        CancellationToken token = cancellation.Begin();

        Assert.False(token.IsCancellationRequested);
        cancellation.Cancel();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_with_the_single_live_run_cancels_it()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = LongDelay;
        TaskId id = environment.Submit("长任务");
        environment.Wait(id, snapshot => snapshot.LiveRuns > 0);

        using TurnCancellation cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("已取消 run #1。", output.Output);
    }

    [Fact]
    public void Cancel_with_several_live_runs_asks_to_pick_one()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Executor.DelayMs = LongDelay;
        TaskId first = environment.Submit("甲");
        TaskId second = environment.Submit("乙");
        environment.Wait(first, snapshot => snapshot.LiveRuns > 0);
        environment.Wait(second, snapshot => snapshot.LiveRuns > 0);

        using TurnCancellation cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("有 2 个 run 在跑", output.Output);
    }

    [Fact]
    public void Cancel_with_nothing_running_says_so()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        using TurnCancellation cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("没有可中断的执行。", output.Output);
    }

    private static TurnCancellation NewCancellation(TestEnvironment environment, out TestConsole output)
    {
        Terminal terminal = CliSession.NewTerminal(out output, out _);

        return new TurnCancellation(environment.Registry, terminal);
    }
}
