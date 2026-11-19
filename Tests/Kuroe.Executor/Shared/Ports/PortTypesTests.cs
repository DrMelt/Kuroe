using Kuroe.Executor.Shared.Ports;
using Xunit;

namespace Kuroe.Executor.Shared.Tests.Ports;

/// <summary>端口类型注册：类型只表达名字与语义，模式由类型注册声明支持范围。</summary>
public sealed class PortTypesTests
{
    [Fact(DisplayName = "内置类型声明支持模式与默认值")]
    public void Built_in_types_declare_supported_modes_and_defaults()
    {
        Assert.Multiple(
            () => Assert.Equal("Text", PortTypes.Text.Name),
            () => Assert.Equal(PortSemantics.Text, PortTypes.Text.Semantics),
            () => Assert.Equal(PortMode.Plain, PortTypes.DefaultMode(PortTypes.Text)),
            () => Assert.True(PortTypes.Supports(PortTypes.Text, PortMode.Plain)),
            () => Assert.True(PortTypes.Supports(PortTypes.Text, PortMode.Trigger)),
            () => Assert.True(PortTypes.Supports(PortTypes.Text, PortMode.Constant)));

        Assert.Multiple(
            () => Assert.Equal("Context", PortTypes.Context.Name),
            () => Assert.Equal(PortSemantics.Context, PortTypes.Context.Semantics),
            () => Assert.Equal(PortMode.Plain, PortTypes.DefaultMode(PortTypes.Context)),
            () => Assert.True(PortTypes.Supports(PortTypes.Context, PortMode.Plain)),
            () => Assert.False(PortTypes.Supports(PortTypes.Context, PortMode.Trigger)),
            () => Assert.True(PortTypes.Supports(PortTypes.Context, PortMode.Constant)));

        Assert.Multiple(
            () => Assert.Equal("Signal", PortTypes.Signal.Name),
            () => Assert.Equal(PortSemantics.Signal, PortTypes.Signal.Semantics),
            () => Assert.Equal(PortMode.Plain, PortTypes.DefaultMode(PortTypes.Signal)),
            () => Assert.True(PortTypes.Supports(PortTypes.Signal, PortMode.Plain)),
            () => Assert.True(PortTypes.Supports(PortTypes.Signal, PortMode.Trigger)),
            () => Assert.True(PortTypes.Supports(PortTypes.Signal, PortMode.Constant)));
    }

    [Fact(DisplayName = "解析不区分大小写且类型不带模式后缀")]
    public void Parse_is_case_insensitive_and_types_have_no_mode_suffixes()
    {
        Assert.True(PortTypes.TryParse("text", out PortType parsed));
        Assert.Equal(PortTypes.Text, parsed);
        Assert.False(PortTypes.TryParse("TextTrigger", out _));
        Assert.False(PortTypes.TryParse("TextConstant", out _));
        Assert.False(PortTypes.TryParse("Unknown", out _));
        Assert.False(PortTypes.TryParse(null, out _));
    }

    [Fact(DisplayName = "注册声明模式并拒绝非法注册")]
    public void Register_declares_modes_and_rejects_invalid_registration()
    {
        PortType audit = PortTypes.Register("Audit", PortSemantics.Text, PortMode.Plain,
            PortMode.Plain, PortMode.Constant);

        Assert.Equal("Audit", audit.Name);
        Assert.Equal(PortSemantics.Text, audit.Semantics);
        Assert.Equal(PortMode.Plain, PortTypes.DefaultMode(audit));
        Assert.True(PortTypes.Supports(audit, PortMode.Plain));
        Assert.False(PortTypes.Supports(audit, PortMode.Trigger));
        Assert.True(PortTypes.Supports(audit, PortMode.Constant));
        Assert.True(PortTypes.TryParse("audit", out PortType parsed));
        Assert.Equal(audit, parsed);

        Assert.Throws<InvalidOperationException>(() =>
            PortTypes.Register("Audit", PortSemantics.Text, PortMode.Plain, PortMode.Plain));
        Assert.Throws<ArgumentException>(() =>
            PortTypes.Register("EmptyModes", PortSemantics.Text, PortMode.Plain));
        Assert.Throws<ArgumentException>(() =>
            PortTypes.Register("BadDefault", PortSemantics.Text, PortMode.Trigger, PortMode.Plain));
        Assert.Throws<ArgumentException>(() =>
            PortTypes.Register(" ", PortSemantics.Text, PortMode.Plain, PortMode.Plain));
    }
}