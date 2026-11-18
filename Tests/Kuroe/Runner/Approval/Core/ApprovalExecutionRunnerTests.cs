using ErrorOr;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.Model;
using Kuroe.Input.Shared;
using Kuroe.Tasks.Shared;
using Kuroe.TestSupport;
using Xunit;
using Kuroe.Execution.Shared.Contract;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Runner.Approval.Tests;

/// <summary>审批节点的执行运行器：登记等待批准，批准后交回空产出，取消时撤销登记。</summary>
public sealed class ApprovalExecutionRunnerTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "审阅");
    private static readonly FlowStepPath Step = NodeStepPaths.From(Path);

    [Fact(DisplayName = "执行声明审批并返回空输出")]
    public async Task Execution_declares_approval_and_returns_an_empty_output()
    {
        StubInputChannel channel = new();
        ApprovalExecutionRunner runner = new(
            channel,
            new ApprovalExecutionParameters("确认计划", Step),
            new Entry(Request()));

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(CancellationToken.None);

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

    [Fact(DisplayName = "审批执行取消拒绝等待")]
    public async Task Cancellation_declines_the_wait()
    {
        StubInputChannel channel = new();
        ApprovalExecutionRunner runner = new(
            channel,
            new ApprovalExecutionParameters(null, Step),
            new Entry(Request()));
        using CancellationTokenSource cancellation = new();

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal([Step], channel.Declined);
    }

    private static ExecutionRequest Request() =>
        new(
            new ExecutionOwnerId("task-1"),
            ExecutionStepPaths.From(Path),
            [new ExecutionInputPort(new ExecutionPortName("正文"), ExecutionPortKind.Text, ExecutionPortRole.Content, true, null)],
            [new ExecutionInputValue(new ExecutionPortName("正文"), "内容", null)],
            new Dictionary<ExecutionPortName, string> { [new ExecutionPortName("正文")] = "内容" },
            []);

    private sealed class Entry(ExecutionRequest request) : IExecutionRequestEntry
    {
        public ExecutionId Id => new(1);

        ExecutionRequest IExecutionRequestEntry.Request => request;

        public ExecutionStatus ReadStatus() => throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            throw new NotSupportedException();
    }
}
