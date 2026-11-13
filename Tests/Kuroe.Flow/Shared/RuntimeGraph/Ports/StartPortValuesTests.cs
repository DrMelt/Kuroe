using Kuroe.Flow.Node.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.RuntimeGraph.Ports;

/// <summary>开始端口文本视图：只覆盖承载内容的开始端口，取值按固定值、宿主给值、提交输入排序。</summary>
public sealed class StartPortValuesTests
{
    private static readonly PortName Goal = new("目标");
    private static readonly PortName Signal = new("启动");
    private static readonly PortName Model = new("模型");

    [Fact]
    public void Signal_start_port_stays_out_of_the_view()
    {
        StartPortValues values = new(
            [new Port(Goal, PortTypes.Text), new Port(Signal, PortTypes.Signal)],
            PortTexts.Empty,
            new StartInputs([new StartInput(Goal, "做"), new StartInput(Signal, "开始")]));

        Assert.Equal("做", values.Text(Goal));
        Assert.Null(values.Text(Signal));
    }

    [Fact]
    public void Fixed_value_wins_over_host_and_submitted_text()
    {
        PortName port = new("分支");
        StartPortValues values = new(
            [new Port(port, PortTypes.Text, value: "固定", mode: PortMode.Constant)],
            new PortTexts(new Dictionary<PortName, string> { [port] = "宿主" }),
            StartInputs.Single(port, "提交"));

        Assert.Equal("固定", values.Text(port));
    }

    [Fact]
    public void Host_text_wins_over_submitted_text()
    {
        StartPortValues values = new(
            [new Port(Model, PortTypes.Text)],
            new PortTexts(new Dictionary<PortName, string> { [Model] = "宿主" }),
            StartInputs.Single(Model, "提交"));

        Assert.Equal("宿主", values.Text(Model));
    }

    [Fact]
    public void Constant_content_port_takes_submitted_text()
    {
        PortName context = new("背景");
        StartPortValues values = new(
            [new Port(context, PortTypes.Context, mode: PortMode.Constant)],
            PortTexts.Empty,
            StartInputs.Single(context, "提交"));

        Assert.Equal("提交", values.Text(context));
    }
}