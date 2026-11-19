using Kuroe.Flow.Graph.Shared.Model;
using Kuroe.Flow.Graph.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Graph.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Graph.Shared.Tests.StaticGraph.Ports;

/// <summary>端口与上游连接的展示名：类型、模式、说明、默认值与翻转条件按各自规则标出。</summary>
public sealed class PortLabelsTests
{
    private static readonly PortName Result = new("结论");

    [Fact(DisplayName = "负向来源追加翻转标记")]
    public void Negative_source_appends_the_flip_marker()
    {
        FromLink link = new(
            Result,
            PortRef.Named(new NodeLabel("来源"), Result),
            TriggerCondition.Negative);

        Assert.Equal("来源@结论·翻转", link.SourceLabel());
    }

    [Fact(DisplayName = "正向来源只显示引用")]
    public void Positive_source_shows_only_the_reference()
    {
        FromLink link = new(Result, PortRef.Named(new NodeLabel("来源"), Result));

        Assert.Equal("来源@结论", link.SourceLabel());
    }

    [Fact(DisplayName = "输出标签标记触发与信号模式")]
    public void Output_label_marks_trigger_and_signal_modes()
    {
        Assert.Equal(
            "结论·触发：是否通过",
            new Port(new PortName("结论"), PortTypes.Text, "是否通过", mode: PortMode.Trigger).Label());
        Assert.Equal(
            "通过·Signal·触发",
            new Port(new PortName("通过"), PortTypes.Signal, mode: PortMode.Trigger).Label());
        Assert.Equal(
            "完成·Signal",
            new Port(new PortName("完成"), PortTypes.Signal).Label());
    }

    [Fact(DisplayName = "输入标签标记触发与信号模式")]
    public void Input_label_marks_trigger_and_signal_modes()
    {
        Assert.Equal(
            "结论·触发",
            new InputPort(new PortName("结论"), PortTypes.Text, mode: PortMode.Trigger).Label());
        Assert.Equal(
            "信号·Signal·触发",
            new InputPort(new PortName("信号"), PortTypes.Signal, mode: PortMode.Trigger).Label());
    }

    [Fact(DisplayName = "开始标签显示默认值")]
    public void Start_label_shows_the_default_value()
    {
        Port port = new(
            new PortName("分支"),
            PortTypes.Text,
            mode: PortMode.Constant,
            defaultValue: "main");

        Assert.Equal("分支，默认 main·常量", port.StartLabel());
    }
}