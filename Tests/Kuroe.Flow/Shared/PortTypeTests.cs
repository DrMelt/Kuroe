using Kuroe.Flow.Shared.StaticGraph.PortModel;
using Xunit;

namespace Kuroe.Flow.Shared.Tests;

/// <summary>端口类型注册：注册一次产生普通版与 Trigger 版，名字唯一且解析不区分大小写。</summary>
public sealed class PortTypeTests
{
    [Fact]
    public void Built_in_types_pair_plain_and_trigger_versions()
    {
        Assert.Equal("Text", PortTypes.Text.Name);
        Assert.Equal(PortSemantics.Text, PortTypes.Text.Semantics);
        Assert.False(PortTypes.Text.IsTrigger);

        Assert.Equal("TextTrigger", PortTypes.TextTrigger.Name);
        Assert.Equal(PortSemantics.Text, PortTypes.TextTrigger.Semantics);
        Assert.True(PortTypes.TextTrigger.IsTrigger);

        Assert.True(PortTypes.SignalTrigger.IsTrigger);
        Assert.True(PortTypes.ContextTrigger.IsTrigger);
    }

    [Fact]
    public void Parse_is_case_insensitive_and_rejects_unknown_names()
    {
        Assert.True(PortTypes.TryParse("texttrigger", out PortType parsed));
        Assert.Equal(PortTypes.TextTrigger, parsed);
        Assert.False(PortTypes.TryParse("Unknown", out _));
        Assert.False(PortTypes.TryParse(null, out _));
    }

    [Fact]
    public void Register_produces_two_types_and_rejects_a_duplicate_name()
    {
        PortTypePair pair = PortTypes.Register("Audit", PortSemantics.Text);

        Assert.Equal("Audit", pair.Plain.Name);
        Assert.Equal("AuditTrigger", pair.Trigger.Name);
        Assert.True(pair.Trigger.IsTrigger);
        Assert.True(PortTypes.TryParse("AuditTrigger", out PortType parsed));
        Assert.Equal(pair.Trigger, parsed);

        Assert.Throws<InvalidOperationException>(() => PortTypes.Register("Audit", PortSemantics.Text));
        Assert.Throws<ArgumentException>(() => PortTypes.Register("AuditTrigger", PortSemantics.Text));
        Assert.Throws<ArgumentException>(() => PortTypes.Register(" ", PortSemantics.Text));
    }

    [Fact]
    public void Submission_covers_text_and_signal_semantics()
    {
        Assert.True(Ports.RequiresSubmission(PortTypes.Text));
        Assert.True(Ports.RequiresSubmission(PortTypes.TextTrigger));
        Assert.True(Ports.RequiresSubmission(PortTypes.SignalTrigger));
        Assert.False(Ports.RequiresSubmission(PortTypes.Context));
        Assert.True(Ports.CarriesText(PortTypes.TextTrigger));
        Assert.False(Ports.CarriesText(PortTypes.SignalTrigger));
    }
}