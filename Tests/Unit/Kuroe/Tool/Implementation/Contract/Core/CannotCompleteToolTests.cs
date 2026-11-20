using ErrorOr;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Tool.Implementation.Contract.Tests;

/// <summary>上报无法完成的载体：回执带上原因与本轮尚缺的端口。</summary>
public sealed class CannotCompleteToolTests
{
    [Fact(DisplayName = "报告渲染原因与剩余端口")]
    public void Report_renders_the_reason_and_the_remaining_ports()
    {
        FakeEntry submitter = new()
        {
            Status = new ExecutionStatus(
                new ExecutionStepId("节点"), ExecutionState.Failed, [new ExecutionPortName("结论")], "缺少输入资料"),
        };
        CannotCompleteTool tool = new(submitter);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["reason"] = "缺少输入资料" }));

        Assert.False(result.IsError);
        Assert.Contains("已记录本轮无法完成：缺少输入资料。", result.Value, StringComparison.Ordinal);
        Assert.Contains("端口「结论」", result.Value, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "报告错误返回给模型")]
    public void Report_error_is_returned_to_the_model()
    {
        FakeEntry submitter = new()
        {
            Failure = Error.Validation("CannotComplete.Reason", "上报无法完成时必须给出原因。"),
        };
        CannotCompleteTool tool = new(submitter);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.True(result.IsError);
        Assert.Equal("上报无法完成时必须给出原因。", result.FirstError.Description);
    }

    private sealed class FakeEntry : IExecutionRequestEntry
    {
        public ExecutionId Id => new(1);

        ExecutionRequest IExecutionRequestEntry.Request => throw new NotSupportedException();

        public ExecutionStatus ReadStatus() => Status;

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ExecutionStatus Status { get; init; } =
            new(new ExecutionStepId("节点"), ExecutionState.Running, [], null);

        public Error? Failure { get; init; }

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            Failure is { } error ? error : Status;
    }
}
