using Kuroe.Flow.Runtime.Nodes;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>输出触发判定：直接构造端口配置与产出触发值，断言下游是否启动。</summary>
public sealed class TriggerRulesTests
{
    private static readonly PortName Conclusion = new("结论");

    [Fact]
    public void Silent_port_always_fires()
    {
        Assert.True(TriggerRules.Output(Text(), new PortValue("内容", false)));
    }

    [Fact]
    public void Trigger_port_uses_the_submitted_value()
    {
        Assert.True(TriggerRules.Output(TriggerText(), new PortValue("内容", true)));
        Assert.False(TriggerRules.Output(TriggerText(), new PortValue("内容", false)));
    }

    [Fact]
    public void Trigger_port_fires_without_a_value()
    {
        Assert.True(TriggerRules.Output(TriggerText(), null));
        Assert.True(TriggerRules.Output(TriggerText(), new PortValue("内容")));
    }

    [Fact]
    public void Signal_port_carries_trigger_by_default()
    {
        Assert.False(TriggerRules.Output(new Port(Conclusion, PortTypes.Signal), new PortValue(null, false)));
    }

    [Fact]
    public void Plain_container_output_ignores_the_member_value()
    {
        Assert.True(TriggerRules.Container(Text(), true, false));
    }

    [Fact]
    public void Triggered_container_output_forwards_the_member_value()
    {
        Assert.False(TriggerRules.Container(TriggerText(), true, false));
        Assert.True(TriggerRules.Container(TriggerText(), true, true));
    }

    [Fact]
    public void Unreleased_container_output_fires()
    {
        Assert.True(TriggerRules.Container(TriggerText(), false, false));
    }

    private static Port Text() => new(Conclusion, PortTypes.Text);

    private static Port TriggerText() => new(Conclusion, PortTypes.Text, trigger: true);
}
