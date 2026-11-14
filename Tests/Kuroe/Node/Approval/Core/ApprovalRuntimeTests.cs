using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;
using ExecutableNode = Kuroe.Flow.Node.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点的运行期对象：取消后不再声明接收外部输入。</summary>
public sealed class ApprovalRuntimeTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "审阅");
    private static readonly PortName Port = new("正文");

    [Fact]
    public void Activation_declares_the_configured_question()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(new ApprovalParameters { Question = "确认计划" }, channel)
                .Create(Node(channel), context));

        runtime.Activate(context, NodeInputValues.Empty);

        Assert.Equal("确认计划", Assert.Single(channel.Declared).Question);
    }

    [Fact]
    public void Canceled_node_never_declares_again()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(null, channel).Create(Node(channel), context));

        runtime.Cancel();
        runtime.Activate(context, NodeInputValues.Empty);

        Assert.Empty(channel.Declared);
        Assert.Equal([Path], channel.Declined);
    }

    [Fact]
    public void Approval_releases_the_values_saved_at_pause()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(null, channel).Create(Node(channel), context));
        PortName port = Port;
        NodeInputValues inputs = new(
            [new NodeInputValue(port, PortTypes.Text, new ContextValue(new StartSource(port), "内容"))],
            new HashSet<PortName> { port });

        runtime.Activate(context, inputs);
        Assert.Single(channel.Declared).Receive(string.Empty);

        Assert.True(runtime.Released);
        Assert.Equal("内容", runtime.PortText(port));
        Assert.Single(runtime.OutputValues(port));
    }

    [Fact]
    public void Approval_outputs_always_trigger()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(null, channel).Create(Node(channel), context));
        NodeInputValues inputs = new(
            [new NodeInputValue(Port, PortTypes.Text, new ContextValue(new StartSource(Port), "内容"))],
            new HashSet<PortName> { Port });

        runtime.Activate(context, inputs);
        Assert.True(runtime.Triggers(Port));
        Assert.Single(channel.Declared).Receive(string.Empty);

        Assert.True(runtime.Triggers(Port));
    }

    private static ExecutableNode Node(IInputChannel channel) => new()
    {
        Path = Path,
        Type = new ApprovalNodeType(null, channel),
        Spec = new ExecutableSpec(),
        Inputs = [new InputPort(Port, PortTypes.Text)],
        Outputs = [],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    };
}