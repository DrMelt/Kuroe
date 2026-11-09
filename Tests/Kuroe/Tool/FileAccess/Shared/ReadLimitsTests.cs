using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Shared.Tests;

/// <summary>读取上限的解析：缺省值、总上限边界与单行上限。</summary>
public sealed class ReadLimitsTests
{
    [Fact]
    public void Missing_values_take_the_defaults()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given()));

        Assert.Equal(
            new ReadLimits(ReadLimits.DefaultCharacters, ReadLimits.DefaultLineCharacters),
            limits);
    }

    [Fact]
    public void Explicit_values_override_the_defaults()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxChars", "12000"), ("maxLineChars", "500"))));

        Assert.Equal(new ReadLimits(12000, 500), limits);
    }

    [Fact]
    public void Total_limit_accepts_the_maximum()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxChars", "50000"))));

        Assert.Equal(ReadLimits.MaximumCharacters, limits.Characters);
    }

    [Fact]
    public void Total_limit_rejects_values_above_the_maximum()
    {
        ErrorOr<ReadLimits> limits = ReadLimits.Parse(Given(("maxChars", "50001")));

        Assert.True(limits.IsError);
        Assert.Contains("maxChars", limits.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_limit_has_no_upper_bound()
    {
        ReadLimits limits = Ok(ReadLimits.Parse(Given(("maxLineChars", "50001"))));

        Assert.Equal(50001, limits.LineCharacters);
    }

    [Theory]
    [InlineData("maxChars", "x", "要写成数字")]
    [InlineData("maxChars", "0", "maxChars")]
    [InlineData("maxChars", "50001", "maxChars")]
    [InlineData("maxLineChars", "x", "要写成数字")]
    [InlineData("maxLineChars", "0", "maxLineChars")]
    [InlineData("maxLineChars", "-1", "maxLineChars")]
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