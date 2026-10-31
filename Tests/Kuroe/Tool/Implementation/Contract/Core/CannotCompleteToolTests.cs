using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Tool.Contract.Tests;

/// <summary>上报无法完成的载体：回执带上原因与本轮尚缺的端口。</summary>
public sealed class CannotCompleteToolTests
{
    [Fact]
    public void Report_renders_the_reason_and_the_remaining_ports()
    {
        FakeSubmitter submitter = new()
        {
            Status = new ExecutionStatus(
                NodePath.Of("节点"), 1, 3, NodeState.Blocked, [new PortName("结论")], "缺少输入资料"),
        };
        CannotCompleteTool tool = new(submitter, new ExecutionId(1));

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["reason"] = "缺少输入资料" }));

        Assert.False(result.IsError);
        Assert.Contains("已记录本轮无法完成：缺少输入资料。", result.Value, StringComparison.Ordinal);
        Assert.Contains("端口「结论」", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_error_is_returned_to_the_model()
    {
        FakeSubmitter submitter = new()
        {
            Failure = Error.Validation("CannotComplete.Reason", "上报无法完成时必须给出原因。"),
        };
        CannotCompleteTool tool = new(submitter, new ExecutionId(1));

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.True(result.IsError);
        Assert.Equal("上报无法完成时必须给出原因。", result.FirstError.Description);
    }

    private sealed class FakeSubmitter : ICannotCompleteSubmitter
    {
        public ExecutionStatus Status { get; init; } = new(NodePath.Of("节点"), 1, 3, NodeState.Running, [], null);

        public Error? Failure { get; init; }

        public ErrorOr<ExecutionStatus> Report(ExecutionId id, string? reason) => Failure is { } error ? error : Status;
    }
}
