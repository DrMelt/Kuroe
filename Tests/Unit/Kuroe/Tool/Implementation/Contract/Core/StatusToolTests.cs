using ErrorOr;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Contract;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Tool.Implementation.Contract.Tests;

/// <summary>查看本轮契约状态的载体：报出步骤、轮次、状态、缺口与已上报的无法完成原因。</summary>
public sealed class StatusToolTests
{
    [Fact(DisplayName = "描述渲染回合契约")]
    public void Describe_renders_the_round_contract()
    {
        StatusTool tool = new(
            new FakeEntry(new ExecutionStatus(
                new ExecutionStepId("节点"), ExecutionState.Failed, [new ExecutionPortName("结论")], "缺少输入资料")),
            2,
            5);

        ErrorOr<string> result = Assert.Single(tool.Functions).Invoke(new ToolArguments(new Dictionary<string, object?>()));

        Assert.False(result.IsError);
        Assert.Contains("步骤 节点 · 第 2 轮 · 执行上限 5", result.Value, StringComparison.Ordinal);
        Assert.Contains("本轮仍需交回：端口「结论」", result.Value, StringComparison.Ordinal);
        Assert.Contains("已上报无法完成：缺少输入资料", result.Value, StringComparison.Ordinal);
    }

    private sealed class FakeEntry(ExecutionStatus status) : IExecutionRequestEntry
    {
        public ExecutionId Id => new(1);

        ExecutionRequest IExecutionRequestEntry.Request => throw new NotSupportedException();

        public ExecutionStatus ReadStatus() => status;

        public ErrorOr<ExecutionStatus> SubmitOutputs(IReadOnlyList<ExecutionOutputValue> values) =>
            throw new NotSupportedException();

        public ErrorOr<ExecutionStatus> ReportCannotComplete(string reason) =>
            throw new NotSupportedException();
    }
}
