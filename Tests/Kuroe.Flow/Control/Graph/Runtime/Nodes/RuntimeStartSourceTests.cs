using Kuroe.Flow.Control.Graph.Runtime.Nodes;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Graph.Tests.Runtime.Nodes;

/// <summary>任务内开始来源：任务启动即放行，只有动态承载内容的端口给出内容。</summary>
public sealed class RuntimeStartSourceTests
{
    private static readonly PortName Goal = new("目标");
    private static readonly PortName Signal = new("启动");
    private static readonly PortName Model = new("模型");

    [Fact]
    public void Source_is_released_with_a_single_revision()
    {
        RuntimeStartSource source = Source();

        Assert.True(source.Released);
        Assert.Equal(1, source.Revision);
        Assert.True(source.Triggers(Signal));
    }

    [Fact]
    public void Dynamic_content_port_carries_the_start_text()
    {
        RuntimeStartSource source = Source();

        ContextValue value = Assert.Single(source.OutputValues(Goal));

        Assert.Equal("做", value.Text);
        Assert.Equal(Goal, Assert.IsType<StartSource>(value.Origin).Port);
    }

    [Fact]
    public void Signal_and_constant_ports_carry_no_content()
    {
        RuntimeStartSource source = Source();

        Assert.Empty(source.OutputValues(Signal));
        Assert.Empty(source.OutputValues(Model));
    }

    private static RuntimeStartSource Source() => new(
        [
            new Port(Goal, PortTypes.Text),
            new Port(Signal, PortTypes.Signal),
            new Port(Model, PortTypes.Text, value: "固定", mode: PortMode.Constant),
        ],
        new PortTexts(new Dictionary<PortName, string>
        {
            [Goal] = "做",
            [Signal] = "开始",
            [Model] = "固定",
        }));
}