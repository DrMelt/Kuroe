using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Ports;

/// <summary>端口规则：交回判定按端口语义、模式与产出模式给出，原始名清单按声明序生成。</summary>
public sealed class PortRulesTests
{
    [Fact]
    public void Submission_covers_text_and_signal_semantics()
    {
        Assert.True(PortRules.RequiresSubmission(PortTypes.Text));
        Assert.True(PortRules.RequiresSubmission(PortTypes.Signal));
        Assert.False(PortRules.RequiresSubmission(PortTypes.Context));
        Assert.True(PortRules.CarriesText(PortTypes.Text));
        Assert.False(PortRules.CarriesText(PortTypes.Signal));
        Assert.True(PortRules.CarriesContent(PortTypes.Text));
        Assert.True(PortRules.CarriesContent(PortTypes.Context));
        Assert.False(PortRules.CarriesContent(PortTypes.Signal));
    }

    [Fact]
    public void Runtime_constant_ports_are_submittable_only_for_once_nodes()
    {
        Port constant = new(new PortName("常量"), PortTypes.Text, mode: PortMode.Constant);

        Assert.False(PortRules.RequiresSubmission(constant, NodeEmission.Repeated));
        Assert.True(PortRules.RequiresSubmission(constant, NodeEmission.Once));
        Assert.Empty(PortRules.SubmittablePorts([constant], NodeEmission.Repeated));
        Assert.Single(PortRules.SubmittablePorts([constant], NodeEmission.Once));
    }

    [Fact]
    public void Submission_shape_classifies_content_and_trigger()
    {
        Assert.Equal(PortSubmissionShape.Content, new Port(new PortName("文本"), PortTypes.Text).SubmissionShape());
        Assert.Equal(PortSubmissionShape.Content, new Port(new PortName("上下文"), PortTypes.Context).SubmissionShape());
        Assert.Equal(PortSubmissionShape.Signal, new Port(new PortName("信号"), PortTypes.Signal).SubmissionShape());
        Assert.Equal(
            PortSubmissionShape.TriggeredContent,
            new Port(new PortName("文本"), PortTypes.Text, mode: PortMode.Trigger).SubmissionShape());
        Assert.Equal(
            PortSubmissionShape.TriggeredSignal,
            new Port(new PortName("信号"), PortTypes.Signal, mode: PortMode.Trigger).SubmissionShape());
    }

    [Fact]
    public void Names_list_the_original_port_names()
    {
        Assert.Equal(
            "结论、通过、完成",
            PortRules.Names([
                new Port(new PortName("结论"), PortTypes.Text, mode: PortMode.Trigger),
                new Port(new PortName("通过"), PortTypes.Signal, mode: PortMode.Trigger),
                new Port(new PortName("完成"), PortTypes.Signal)
            ]));
    }
}
