using ErrorOr;
using Kuroe.Execution.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Runner.Approval.Tests;

/// <summary>审批节点的执行运行器：登记等待批准，批准后交回空产出，取消时撤销登记。</summary>
public sealed class ApprovalExecutionRunnerTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "审阅");
    private static readonly FlowStepPath Step = NodeStepPaths.From(Path);

    [Fact]
    public async Task Execution_declares_approval_and_returns_an_empty_output()
    {
        StubInputChannel channel = new();
        ApprovalExecutionRunner runner = new(channel, new ApprovalExecutionParameters("确认计划", Step));

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(Request(), CancellationToken.None);

        (FlowStepPath step, FlowPendingKind kind, string? question, InputReceiver receive) = Assert.Single(channel.Declared);
        Assert.Equal(Step, step);
        Assert.Equal(FlowPendingKind.Approve, kind);
        Assert.Equal("确认计划", question);

        receive(string.Empty);

        ErrorOr<ExecutionOutput> outcome = await running;
        Assert.False(outcome.IsError);
        Assert.Empty(outcome.Value.Values);
        Assert.Empty(channel.Declined);
    }

    [Fact]
    public async Task Cancellation_declines_the_wait()
    {
        StubInputChannel channel = new();
        ApprovalExecutionRunner runner = new(channel, new ApprovalExecutionParameters(null, Step));
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
            "Approval",
            1,
            [new ExecutionInputPort(new ExecutionPortName("正文"), ExecutionPortKind.Text, ExecutionPortRole.Content, true, null)],
            [new ExecutionInputValue(new ExecutionPortName("正文"), "内容", null)],
            new Dictionary<ExecutionPortName, string> { [new ExecutionPortName("正文")] = "内容" },
            []);
}