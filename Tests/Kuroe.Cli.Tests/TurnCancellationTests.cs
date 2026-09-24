using Kuroe.Cli;
using Kuroe.Shared.Agent;
using Kuroe.TestSupport;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>Ctrl+C 的取消范围：先前台对话，其次唯一的在跑 agent，多个时只给出提示。</summary>
public sealed class TurnCancellationTests
{
    private const int LongDelay = 20000;

    [Fact]
    public void Begin_hands_out_a_token_and_cancel_triggers_it()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        using TurnCancellation cancellation = NewCancellation(harness, out _);

        CancellationToken token = cancellation.Begin();

        Assert.False(token.IsCancellationRequested);
        cancellation.Cancel();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_with_the_single_live_run_cancels_it()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = LongDelay;
        TaskId id = harness.Submit("长任务");
        harness.Wait(id, snapshot => snapshot.LiveRuns > 0);

        using TurnCancellation cancellation = NewCancellation(harness, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("已取消 agent #1。", output.Output);
    }

    [Fact]
    public void Cancel_with_several_live_runs_asks_to_pick_one()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        harness.Executor.DelayMs = LongDelay;
        TaskId first = harness.Submit("甲");
        TaskId second = harness.Submit("乙");
        harness.Wait(first, snapshot => snapshot.LiveRuns > 0);
        harness.Wait(second, snapshot => snapshot.LiveRuns > 0);

        using TurnCancellation cancellation = NewCancellation(harness, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("有 2 个 agent 在跑", output.Output);
    }

    [Fact]
    public void Cancel_with_nothing_running_says_so()
    {
        using KuroeHarness harness = KuroeHarness.Create();

        using TurnCancellation cancellation = NewCancellation(harness, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("没有可中断的执行。", output.Output);
    }

    private static TurnCancellation NewCancellation(KuroeHarness harness, out TestConsole output)
    {
        Terminal terminal = Ui.NewTerminal(out output, out _);

        return new TurnCancellation(harness.Registry, terminal);
    }
}