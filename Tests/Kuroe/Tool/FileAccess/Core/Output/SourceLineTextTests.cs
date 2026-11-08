using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests.Output;

/// <summary>源行文本：行号对齐与终止符处理。</summary>
public sealed class SourceLineTextTests
{
    [Fact]
    public void ToString_aligns_numbers_and_appends_each_terminator()
    {
        SourceLine[] lines = [new(8, "L8", NewLineStyle.CrLf), new(12, "L12", NewLineStyle.Lf)];

        Assert.Equal(" 8 |L8\r\n12 |L12\n", new SourceLineText(lines, true).ToString());
    }

    [Fact]
    public void ToString_without_numbers_keeps_text_and_terminators()
    {
        SourceLine[] lines =
        [
            new(8, "L8", NewLineStyle.CrLf),
            new(12, string.Empty, NewLineStyle.Lf),
            new(13, "L13", null),
        ];

        Assert.Equal("L8\r\n\nL13", new SourceLineText(lines, false).ToString());
    }

    [Fact]
    public void ToString_of_no_lines_is_empty()
    {
        Assert.Equal(string.Empty, new SourceLineText([], true).ToString());
    }
}