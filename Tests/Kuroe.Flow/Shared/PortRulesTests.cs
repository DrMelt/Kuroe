using Kuroe.Flow.Shared.StaticGraph.Ports;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>端口规则：交回判定按端口语义给出。</summary>
public sealed class PortRulesTests
{
    [Fact]
    public void Submission_covers_text_and_signal_semantics()
    {
        Assert.True(PortRules.RequiresSubmission(PortTypes.Text));
        Assert.True(PortRules.RequiresSubmission(PortTypes.TextTrigger));
        Assert.True(PortRules.RequiresSubmission(PortTypes.SignalTrigger));
        Assert.False(PortRules.RequiresSubmission(PortTypes.Context));
        Assert.True(PortRules.CarriesText(PortTypes.TextTrigger));
        Assert.False(PortRules.CarriesText(PortTypes.SignalTrigger));
    }
}
