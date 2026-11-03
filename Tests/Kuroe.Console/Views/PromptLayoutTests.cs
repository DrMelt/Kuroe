using Kuroe.Console.Views;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>输入回显的行数计算：终端宽度与文本宽度的边界。</summary>
public sealed class PromptLayoutTests
{
    [Theory]
    [InlineData(80, 0, 1)]
    [InlineData(80, 79, 1)]
    [InlineData(80, 80, 1)]
    [InlineData(80, 81, 2)]
    [InlineData(80, 160, 2)]
    [InlineData(80, 161, 3)]
    [InlineData(40, 81, 3)]
    [InlineData(1, 3, 3)]
    [InlineData(0, 2, 2)]
    public void Rows_counts_the_rows_the_text_occupies(int width, int textWidth, int expected)
    {
        Assert.Equal(expected, PromptLayout.Rows(width, textWidth));
    }
}