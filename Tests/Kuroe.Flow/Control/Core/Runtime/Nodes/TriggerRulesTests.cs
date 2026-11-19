using Kuroe.Flow.Control.Runtime.Nodes;
using Kuroe.Flow.Graph.Shared.RuntimeGraph.Ports;
using Kuroe.FlowDescription.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Tests.Runtime.Nodes;

/// <summary>输出触发判定：直接构造端口配置与产出触发值，断言下游是否启动。</summary>
public sealed class TriggerRulesTests
{
    private static readonly PortName Conclusion = new("结论");

    [Fact(DisplayName = "静默端口总是发布")]
    public void Silent_port_always_fires()
    {
        Assert.True(TriggerRules.Output(Text(), new PortValue("内容", false)));
    }

    [Fact(DisplayName = "触发端口使用提交值")]
    public void Trigger_port_uses_the_submitted_value()
    {
        Assert.True(TriggerRules.Output(TriggerText(), new PortValue("内容", true)));
        Assert.False(TriggerRules.Output(TriggerText(), new PortValue("内容", false)));
    }

    [Fact(DisplayName = "触发端口没有值也发布")]
    public void Trigger_port_fires_without_a_value()
    {
        Assert.True(TriggerRules.Output(TriggerText(), null));
        Assert.True(TriggerRules.Output(TriggerText(), new PortValue("内容")));
    }

    [Fact(DisplayName = "信号端口默认按普通处理")]
    public void Signal_port_is_plain_by_default()
    {
        Assert.True(TriggerRules.Output(new Port(Conclusion, PortTypes.Signal), new PortValue(null, false)));
    }

    [Fact(DisplayName = "触发信号端口使用提交值")]
    public void Trigger_signal_port_uses_the_submitted_value()
    {
        Port signal = new(Conclusion, PortTypes.Signal, mode: PortMode.Trigger);

        Assert.False(TriggerRules.Output(signal, new PortValue(null, false)));
        Assert.True(TriggerRules.Output(signal, new PortValue(null, true)));
    }

    [Fact(DisplayName = "普通容器输出忽略成员值")]
    public void Plain_container_output_ignores_the_member_value()
    {
        Assert.True(TriggerRules.Container(Text(), true, false));
    }

    [Fact(DisplayName = "触发容器输出转发成员值")]
    public void Triggered_container_output_forwards_the_member_value()
    {
        Assert.False(TriggerRules.Container(TriggerText(), true, false));
        Assert.True(TriggerRules.Container(TriggerText(), true, true));
    }

    [Fact(DisplayName = "未释放的容器输出照常发布")]
    public void Unreleased_container_output_fires()
    {
        Assert.True(TriggerRules.Container(TriggerText(), false, false));
    }

    private static Port Text() => new(Conclusion, PortTypes.Text);

    private static Port TriggerText() => new(Conclusion, PortTypes.Text, mode: PortMode.Trigger);
}
