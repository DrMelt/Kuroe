using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Approval.Tests;

/// <summary>审批节点的运行期对象：取消后不再声明接收外部输入。</summary>
public sealed class ApprovalRuntimeTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "审阅");
    private static readonly PortName Port = new("正文");

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
        Assert.Equal("内容", runtime.PortText(context, port));
        Assert.Single(runtime.OutputValues(context, port));
    }

    [Fact]
    public void Approval_releases_the_trigger_saved_at_pause()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        ApprovalRuntime runtime = Assert.IsType<ApprovalRuntime>(
            new ApprovalNodeType(null, channel).Create(Node(channel), context));
        NodeInputValues inputs = new(
            [new NodeInputValue(Port, PortTypes.TextTrigger, new ContextValue(new StartSource(Port), "内容"))],
            new HashSet<PortName> { Port },
            new Dictionary<PortName, bool> { [Port] = false });

        runtime.Activate(context, inputs);
        Assert.True(runtime.Triggers(Port));
        Assert.Single(channel.Declared).Receive(string.Empty);

        Assert.False(runtime.Triggers(Port));
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