using Kuroe.Flow.Node.Shared.StaticGraph.Nodes;
using Kuroe.Flow.Node.Shared.StaticGraph.Ports;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Shared.Tests.StaticGraph.Ports;

/// <summary>端口规则：交回判定按端口语义、模式与产出模式给出。</summary>
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
}
