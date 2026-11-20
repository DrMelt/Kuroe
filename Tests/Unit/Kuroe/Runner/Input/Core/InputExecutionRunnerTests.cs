using ErrorOr;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Nodes;
using Kuroe.FlowDescription.Definition.Shared.Model;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.TestSupport;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Runner.Input.Tests;

/// <summary>输入节点的执行运行器：登记等待回答，回答作为执行内容交回，取消时撤销登记。</summary>
public sealed class InputExecutionRunnerTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "收话");
    private static readonly FlowStepPath Step = NodeStepPaths.From(Path);

    [Fact(DisplayName = "执行声明接收并返回回答")]
    public async Task Execution_declares_reception_and_returns_the_answer()
    {
        StubInputChannel channel = new();
        InputExecutionRunner runner = new(
            channel,
            new InputExecutionParameters("要做什么", Step),
            new Entry(Request()));

        Task<ErrorOr<ExecutionOutput>> running = runner.ExecuteAsync(CancellationToken.None);

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

    [Fact(DisplayName = "输入执行取消拒绝等待")]
    public async Task Cancellation_declines_the_wait()
    {
        StubInputChannel channel = new();
        InputExecutionRunner runner = new(
            channel,
            new InputExecutionParameters(null, Step),
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
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
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
