using ErrorOr;
using Xunit;

namespace Kuroe.Ability.Tool.FileAccess.Shared.Tests;

/// <summary>读取上限的解析：缺省值、总上限边界与单行上限。</summary>
public sealed class ReadLimitsTests
{
    [Fact(DisplayName = "缺失值采用默认值")]
    public void Missing_values_take_the_defaults()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given()));

        Assert.Equal(
            new ReadLimits(ReadLimits.DefaultCharacters, ReadLimits.DefaultLineCharacters),
            limits);
    }

    [Fact(DisplayName = "读取上限显式值覆盖默认值")]
    public void Explicit_values_override_the_defaults()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxChars", "12000"), ("maxLineChars", "500"))));

        Assert.Equal(new ReadLimits(12000, 500), limits);
    }

    [Fact(DisplayName = "总上限接受最大值")]
    public void Total_limit_accepts_the_maximum()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxChars", "50000"))));

        Assert.Equal(ReadLimits.MaximumCharacters, limits.Characters);
    }

    [Fact(DisplayName = "总上限拒绝超过最大值的值")]
    public void Total_limit_rejects_values_above_the_maximum()
    {
        ErrorOr<ReadLimits> limits = ReadLimits.Parse(Given(("maxChars", "50001")));

        Assert.True(limits.IsError);
        Assert.Contains("maxChars", limits.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "行上限没有上界")]
    public void Line_limit_has_no_upper_bound()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxLineChars", "50001"))));

        Assert.Equal(50001, limits.LineCharacters);
    }

    [Theory]
    [InlineData("maxChars", "x", "要写成数字", Label = "最大字符非数字")]
    [InlineData("maxChars", "0", "maxChars", Label = "最大字符为零")]
    [InlineData("maxChars", "50001", "maxChars", Label = "最大字符超上限")]
    [InlineData("maxLineChars", "x", "要写成数字", Label = "单行最大字符非数字")]
    [InlineData("maxLineChars", "0", "maxLineChars", Label = "单行最大字符为零")]
    [InlineData("maxLineChars", "-1", "maxLineChars", Label = "单行最大字符为负")]
    public void Invalid_values_are_rejected(string name, string value, string expected)
    {
        ErrorOr<ReadLimits> limits = ReadLimits.Parse(Given((name, value)));

        Assert.True(limits.IsError);
        Assert.Contains(expected, limits.FirstError.Description, StringComparison.Ordinal);
    }

    private static ReadLimits Ok(ErrorOr<ReadLimits> limits)
    {
        Assert.False(limits.IsError);

        return limits.Value;
    }

    private static ToolArguments Given(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => (object?)value.Value));
}