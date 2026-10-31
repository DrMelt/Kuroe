using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Tool.Contract.Tests;

/// <summary>查看本轮契约状态的载体：报出节点、轮次、状态、缺口与已上报的无法完成原因。</summary>
public sealed class StatusToolTests
{
    [Fact]
    public void Describe_renders_the_round_contract()
    {
        StatusTool tool = new(
            new FakeReader(new ExecutionStatus(
                NodePath.Of("节点"), 2, 5, NodeState.Blocked, [new PortName("结论")], "缺少输入资料")),
            new ExecutionId(1));

        ErrorOr<string> result = Assert.Single(tool.Functions).Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.False(result.IsError);
        Assert.Contains("节点 节点 · 第 2 轮 · 执行上限 5", result.Value, StringComparison.Ordinal);
        Assert.Contains("本轮仍需交回：端口「结论」", result.Value, StringComparison.Ordinal);
        Assert.Contains("已上报无法完成：缺少输入资料", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_error_is_returned_to_the_model()
    {
        StatusTool tool = new(
            new FakeReader(Error.NotFound("Execution.NotFound", "执行不存在。")),
            new ExecutionId(1));

        ErrorOr<string> result = Assert.Single(tool.Functions).Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.True(result.IsError);
        Assert.Equal("执行不存在。", result.FirstError.Description);
    }

    private sealed class FakeReader(ErrorOr<ExecutionStatus> result) : IExecutionStatusReader
    {
        public ErrorOr<ExecutionStatus> Read(ExecutionId id) => result;
    }
}
