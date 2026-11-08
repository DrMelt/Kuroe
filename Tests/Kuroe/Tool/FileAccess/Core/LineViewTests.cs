using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>行渲染：行号对齐与终止符处理。</summary>
public sealed class LineViewTests
{
    [Fact]
    public void Format_aligns_numbers_and_appends_each_terminator()
    {
        SourceLine[] lines = [new(8, "L8", "\r\n"), new(12, "L12", "\n")];

        Assert.Equal(" 8 |L8\r\n12 |L12\n", LineView.Format(lines));
    }

    [Fact]
    public void Format_without_numbers_keeps_text_and_terminators()
    {
        SourceLine[] lines = [new(8, "L8", "\r\n"), new(12, string.Empty, "\n"), new(13, "L13", string.Empty)];

        Assert.Equal("L8\r\n\nL13", LineView.Format(lines, lineNumbers: false));
    }

    [Fact]
    public void Format_of_numbered_lines_uses_lf_between_lines()
    {
        NumberedLine[] lines = [new(3, "needle"), new(12, "needle")];

        Assert.Equal(" 3 |needle\n12 |needle", LineView.Format(lines));
    }

    [Fact]
    public void Format_of_no_lines_is_empty()
    {
        Assert.Equal(string.Empty, LineView.Format(Array.Empty<SourceLine>()));
        Assert.Equal(string.Empty, LineView.Format([], lineNumbers: false));
        Assert.Equal(string.Empty, LineView.Format([]));
    }
}