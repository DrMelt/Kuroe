using Kuroe.Flow.Shared.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>Ctrl+C 的取消范围：先当前一轮对话请求，其次唯一的在跑执行，多个时只给出提示。</summary>
public sealed class CancellationScopeTests
{
    private const int LongDelay = 20000;

    [Fact]
    public void Begin_hands_out_a_token_and_cancel_triggers_it()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        using CancellationScope cancellation = NewCancellation(environment, out _);

        CancellationToken token = cancellation.Begin();

        Assert.False(token.IsCancellationRequested);
        cancellation.Cancel();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Cancel_with_the_single_live_execution_cancels_it()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = LongDelay;
        TaskId id = environment.Submit("长任务");
        environment.Wait(id, snapshot => snapshot.LiveExecutions > 0);

        using CancellationScope cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains($"已取消 {new ExecutionId(1)}。", output.Output);
    }

    [Fact]
    public void Cancel_with_several_live_executions_asks_to_pick_one()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        environment.Runner.DelayMs = LongDelay;
        TaskId first = environment.Submit("甲");
        TaskId second = environment.Submit("乙");
        environment.Wait(first, snapshot => snapshot.LiveExecutions > 0);
        environment.Wait(second, snapshot => snapshot.LiveExecutions > 0);

        using CancellationScope cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("有 2 个执行在跑", output.Output);
    }

    [Fact]
    public void Cancel_with_nothing_running_says_so()
    {
        using TestEnvironment environment = TestEnvironment.Create();

        using CancellationScope cancellation = NewCancellation(environment, out TestConsole output);
        cancellation.Cancel();

        Assert.Contains("没有可中断的执行。", output.Output);
    }

    private static CancellationScope NewCancellation(TestEnvironment environment, out TestConsole output)
    {
        Terminal terminal = CliSession.NewTerminal(out output, out _);

        return new CancellationScope(environment.Registry, terminal);
    }
}
