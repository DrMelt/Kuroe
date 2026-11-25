using Kuroe.Console.Views;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>输入回显的行数计算：终端宽度与文本宽度的边界。</summary>
public sealed class PromptLayoutTests
{
    [Theory]
    [InlineData(80, 0, 1, Label = "零宽文本")]
    [InlineData(80, 79, 1, Label = "不足一行")]
    [InlineData(80, 80, 1, Label = "恰好一行")]
    [InlineData(80, 81, 2, Label = "超过一行")]
    [InlineData(80, 160, 2, Label = "恰好两行")]
    [InlineData(80, 161, 3, Label = "超过两行")]
    [InlineData(40, 81, 3, Label = "窄终端三行")]
    [InlineData(1, 3, 3, Label = "单列三行")]
    [InlineData(0, 2, 2, Label = "零宽终端")]
    public void Rows_counts_the_rows_the_text_occupies(int width, int textWidth, int expected)
    {
        Assert.Equal(expected, PromptLayout.Rows(width, textWidth));
    }
}