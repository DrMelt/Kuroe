using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>按行读取：终止符识别、行号范围、文件尾与字符预算。</summary>
public sealed class SourceLineReaderTests
{
    [Fact]
    public void Reads_lf_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\n二\n", 1, 10);

        Assert.False(result.FromBeyondEnd);
        Assert.True(result.ToBeyondEnd);
        SourceLine[] expected = [new(1, "一", "\n"), new(2, "二", "\n"), new(3, string.Empty, string.Empty)];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Reads_crlf_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n", 1, 10);

        SourceLine[] expected = [new(1, "一", "\r\n"), new(2, "二", "\r\n"), new(3, string.Empty, string.Empty)];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Reads_cr_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\r二\r", 1, 10);

        SourceLine[] expected = [new(1, "一", "\r"), new(2, "二", "\r"), new(3, string.Empty, string.Empty)];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Keeps_the_terminator_of_each_line_in_a_mixed_file()
    {
        SourceLineReadResult result = Read("一\r\n二\r三\n四", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", "\r\n"),
            new(2, "二", "\r"),
            new(3, "三", "\n"),
            new(4, "四", string.Empty),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Keeps_empty_lines()
    {
        SourceLineReadResult result = Read("一\r\n\r\n二\n", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", "\r\n"),
            new(2, string.Empty, "\r\n"),
            new(3, "二", "\n"),
            new(4, string.Empty, string.Empty),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Last_line_without_a_terminator_has_no_new_line()
    {
        SourceLineReadResult result = Read("一\n二", 1, 10);

        SourceLine[] expected = [new(1, "一", "\n"), new(2, "二", string.Empty)];
        Assert.Equal(expected, result.Lines);
    }

    [Fact]
    public void Trailing_terminator_adds_an_empty_line()
    {
        SourceLineReadResult result = Read("一\n", 1, 10);

        SourceLine[] expected = [new(1, "一", "\n"), new(2, string.Empty, string.Empty)];
        Assert.Equal(expected, result.Lines);
        Assert.True(result.ToBeyondEnd);
    }

    [Fact]
    public void Reads_the_requested_range()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n三\r\n", 2, 3);

        SourceLine[] expected = [new(2, "二", "\r\n"), new(3, "三", "\r\n")];
        Assert.Equal(expected, result.Lines);
        Assert.False(result.ToBeyondEnd);
    }

    [Fact]
    public void Reports_a_start_line_beyond_the_end()
    {
        SourceLineReadResult result = Read("一\r\n", 3, 3);

        Assert.True(result.FromBeyondEnd);
        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void A_start_line_at_the_trailing_empty_line_returns_it()
    {
        SourceLineReadResult result = Read("一\n", 2, 2);

        Assert.False(result.FromBeyondEnd);
        Assert.False(result.ToBeyondEnd);
        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(new SourceLine(2, string.Empty, string.Empty), line);
    }

    [Fact]
    public void A_start_line_after_an_unterminated_last_line_is_beyond_the_end()
    {
        SourceLineReadResult result = Read("一", 2, 50);

        Assert.True(result.FromBeyondEnd);
        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Reports_a_file_end()
    {
        SourceLineReadResult result = Read("一\n二", 1, 10);

        Assert.True(result.ToBeyondEnd);
    }

    [Fact]
    public void Stops_after_the_character_budget()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n", 1, 10, characterBudget: 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(new SourceLine(1, "一", "\r\n"), line);
        Assert.False(result.ToBeyondEnd);
    }

    [Fact]
    public void Handles_crlf_across_the_buffer_boundary()
    {
        string text = new string('a', 4095) + "\r\nb";

        SourceLineReadResult result = Read(text, 1, 2);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(4095, result.Lines[0].Text.Length);
        Assert.Equal("\r\n", result.Lines[0].NewLine);
        Assert.Equal(new SourceLine(2, "b", string.Empty), result.Lines[1]);
    }

    [Fact]
    public void Handles_a_line_longer_than_the_buffer()
    {
        string text = new string('a', 5000) + "\n";

        SourceLineReadResult result = Read(text, 1, 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(5000, line.Text.Length);
        Assert.Equal("\n", line.NewLine);
    }

    [Fact]
    public void Preserves_surrogate_pairs_across_the_buffer()
    {
        string content = new string('a', 4095) + "𠀀";
        SourceLineReadResult result = Read($"{content}\n", 1, 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(content, line.Text);
        Assert.Equal("\n", line.NewLine);
    }

    [Fact]
    public void Reads_an_empty_file()
    {
        SourceLineReadResult result = Read(string.Empty, 1, 10);

        Assert.False(result.FromBeyondEnd);
        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    private static SourceLineReadResult Read(
        string text,
        int from,
        int to,
        int characterBudget = TextLimit.Characters) =>
        SourceLineReader.Read(new StringReader(text), from, to, characterBudget);
}