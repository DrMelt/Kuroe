using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests.Output;

/// <summary>带行号的文本：行号对齐与 LF 分隔。</summary>
public sealed class NumberedLineTextTests
{
    [Fact]
    public void ToString_aligns_numbers_with_lf_between_lines()
    {
        NumberedLine[] lines = [new(3, "needle"), new(12, "needle")];

        Assert.Equal(" 3 |needle\n12 |needle", new NumberedLineText(lines).ToString());
    }

    [Fact]
    public void ToString_of_no_lines_is_empty()
    {
        Assert.Equal(string.Empty, new NumberedLineText([]).ToString());
    }
}