using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Tool.Contract.Tests;

/// <summary>交回端口产出的载体：说明带端口清单，回执给出本轮尚缺的端口。</summary>
public sealed class PortToolTests
{
    private static readonly PortName Conclusion = new("结论");
    private static readonly PortName Reason = new("理由");

    [Fact]
    public void Description_lists_the_ports_to_submit()
    {
        PortTool tool = new(new FakeSubmitter(), new ExecutionId(1), [Port(Conclusion), Port(Reason)]);

        ToolFunction function = Assert.Single(tool.Functions);

        Assert.Contains("结论", function.Definition.Description, StringComparison.Ordinal);
        Assert.Contains("理由", function.Definition.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_renders_the_recorded_ports_and_the_remaining_ones()
    {
        FakeSubmitter submitter = new()
        {
            Outcome = Status([Reason]),
        };
        PortTool tool = new(submitter, new ExecutionId(1), [Port(Conclusion), Port(Reason)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成" }""")));

        Assert.False(result.IsError);
        Assert.Contains("已记录 1 个端口的产出", result.Value, StringComparison.Ordinal);
        Assert.Contains("本轮仍需交回：端口「理由」", result.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_error_is_returned_to_the_model()
    {
        FakeSubmitter submitter = new()
        {
            Failure = Error.Validation("Port.Rejected", "端口值不合法。"),
        };
        PortTool tool = new(submitter, new ExecutionId(1), [Port(Conclusion)]);

        ErrorOr<string> result = Assert.Single(tool.Functions)
            .Invoke(Given(("valuesJson", """{ "结论": "完成" }""")));

        Assert.True(result.IsError);
        Assert.Equal("端口值不合法。", result.FirstError.Description);
    }

    private static Port Port(PortName name) => new(name, PortTypes.Text);

    private static ExecutionStatus Status(IReadOnlyList<PortName> outstanding) =>
        new(NodePath.Of("节点"), 1, 3, NodeState.Running, outstanding, null);

    private static ToolArguments Given(params (string Name, object? Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => value.Value));

    private sealed class FakeSubmitter : IPortSubmitter
    {
        public ExecutionStatus Outcome { get; init; } = new(NodePath.Of("节点"), 1, 3, NodeState.Running, [], null);

        public Error? Failure { get; init; }

        public ErrorOr<ExecutionStatus> SubmitValues(
            ExecutionId id,
            IReadOnlyDictionary<string, PortValue> values) =>
            Failure is { } error ? error : Outcome;
    }
}
