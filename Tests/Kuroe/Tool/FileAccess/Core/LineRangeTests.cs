using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>行号范围的解析：缺省取给定量，非法数字与倒置范围拒绝。</summary>
public sealed class LineRangeTests
{
    [Fact]
    public void Missing_values_take_the_given_defaults()
    {
        LineRange range = Ok(LineRange.Parse(Given(), 1, 200));

        Assert.Equal(new LineRange(1, 200), range);
    }

    [Fact]
    public void Explicit_values_override_the_defaults()
    {
        LineRange range = Ok(LineRange.Parse(Given(("from", "3"), ("to", "9")), 1, 200));

        Assert.Equal(new LineRange(3, 9), range);
    }

    [Theory]
    [InlineData("from", "x", "要写成数字")]
    [InlineData("from", "0", "起始行号")]
    [InlineData("to", "x", "要写成数字")]
    [InlineData("to", "0", "结束行号")]
    public void Invalid_number_is_rejected(string name, string value, string expected)
    {
        ErrorOr<LineRange> range = LineRange.Parse(Given((name, value)), 1, 200);

        Assert.True(range.IsError);
        Assert.Contains(expected, range.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Reversed_range_is_rejected()
    {
        ErrorOr<LineRange> range = LineRange.Parse(Given(("from", "9"), ("to", "3")), 1, 200);

        Assert.True(range.IsError);
        Assert.Contains("不能小于", range.FirstError.Description, StringComparison.Ordinal);
    }

    private static LineRange Ok(ErrorOr<LineRange> range)
    {
        Assert.False(range.IsError);

        return range.Value;
    }

    private static ToolArguments Given(params (string Name, string Value)[] values) =>
        new(values.ToDictionary(value => value.Name, value => (object?)value.Value));
}
