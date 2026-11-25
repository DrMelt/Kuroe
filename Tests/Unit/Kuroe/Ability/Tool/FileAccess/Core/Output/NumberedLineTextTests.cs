using Kuroe.Ability.Tool.FileAccess.Output;
using Kuroe.Ability.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Ability.Tool.FileAccess.Tests.Output;

/// <summary>带行号的文本：行号对齐与 LF 分隔。</summary>
public sealed class NumberedLineTextTests
{
    [Fact(DisplayName = "编号行文本对齐行号并逐行换行")]
    public void ToString_aligns_numbers_with_lf_between_lines()
    {
        NumberedLine[] lines = [new(3, "needle"), new(12, "needle")];

        Assert.Equal(" 3 |needle\n12 |needle", new NumberedLineText(lines).ToString());
    }

    [Fact(DisplayName = "编号行文本没有行时为空")]
    public void ToString_of_no_lines_is_empty()
    {
        Assert.Equal(string.Empty, new NumberedLineText([]).ToString());
    }
}