using Kuroe.Flow.InputChannel.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Shared.StaticGraph.Node;
using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;
using ExecutableNode = Kuroe.Flow.Shared.StaticGraph.Graph.ExecutableNode;

namespace Kuroe.Node.Input.Tests;

/// <summary>输入节点的运行期对象：激活时声明接收外部输入，交付即产出，取消后不再声明。</summary>
public sealed class InputRuntimeTests
{
    private static readonly NodePath Path = NodePath.Of("整体", "收话");

    [Fact]
    public void Activation_declares_reception()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();

        Runtime(channel, context).Activate(context, NodeInputValues.Empty);

        Assert.Equal([Path], [.. channel.Declared.Select(entry => entry.Node)]);
    }

    [Fact]
    public void Delivery_records_the_answer_without_declining()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        InputRuntime runtime = Runtime(channel, context);
        runtime.Activate(context, NodeInputValues.Empty);

        Assert.Single(channel.Declared).Receive("需要支持多语言");

        Assert.True(runtime.Released);
        Assert.Empty(channel.Declined);
    }

    [Fact]
    public void Canceled_node_never_declares_again()
    {
        StubInputChannel channel = new();
        StubNodeContext context = new();
        InputRuntime runtime = Runtime(channel, context);
        runtime.Activate(context, NodeInputValues.Empty);
        runtime.Cancel();

        runtime.Activate(context, NodeInputValues.Empty);

        Assert.Single(channel.Declared);
        Assert.Equal([Path], channel.Declined);
    }

    /// <summary>建出运行期对象，类型与图节点共用同一个协议替身与任务上下文。</summary>
    private static InputRuntime Runtime(IInputChannel channel, INodeContext context) =>
        Assert.IsType<InputRuntime>(new InputNodeType(null, channel).Create(Node(channel), context));

    private static ExecutableNode Node(IInputChannel channel) => new()
    {
        Path = Path,
        Type = new InputNodeType(null, channel),
        Spec = new ExecutableSpec(),
        Inputs = [],
        Outputs = [new Port(new PortName("回答"), PortTypes.Text)],
        MaxExecutions = ExecutableNode.DefaultMaxExecutions,
    };
}