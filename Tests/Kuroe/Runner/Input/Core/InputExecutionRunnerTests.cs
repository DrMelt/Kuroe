using ErrorOr;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Xunit;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Runner.Input.Tests;

/// <summary>输入节点的执行运行器：登记等待回答，回答作为执行内容交回，取消时撤销登记。</summary>
public sealed class InputExecutionRunnerTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "收话");
    private static readonly FlowStepPath Step = NodeStepPaths.From(Path);

    [Fact]
    public async Task Execution_declares_reception_and_returns_the_answer()
    {
        StubInputChannel channel = new();
        InputExecutionRunner runner = new(channel, new InputExecutionParameters("要做什么", Step));

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(Request(), CancellationToken.None);

        (FlowStepPath step, FlowPendingKind kind, string? question, InputReceiver receive) = Assert.Single(channel.Declared);
        Assert.Equal(Step, step);
        Assert.Equal(FlowPendingKind.Input, kind);
        Assert.Equal("要做什么", question);

        receive("需要支持多语言");

        ErrorOr<ExecutionOutput> outcome = await running;
        Assert.False(outcome.IsError);
        Assert.Equal("需要支持多语言", outcome.Value.Content);
        Assert.Empty(channel.Declined);
    }

    [Fact]
    public async Task Cancellation_declines_the_wait()
    {
        StubInputChannel channel = new();
        InputExecutionRunner runner = new(channel, new InputExecutionParameters(null, Step));
        using CancellationTokenSource cancellation = new();

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(Request(), cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal([Step], channel.Declined);
    }

    private static ExecutionRequest Request() =>
        new(
            new ExecutionOwnerId("task-1"),
            ExecutionTargetPaths.Target(Path),
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            []);
}
