using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>按行读取：终止符识别、行号范围、文件尾与字符预算。</summary>
public sealed class SourceLineReaderTests
{
    [Fact(DisplayName = "读取带终止符的 LF 行")]
    public void Reads_lf_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\n二\n", 1, 10);

        Assert.True(result.ToBeyondEnd);
        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.Lf),
            new(2, "二", NewLineStyle.Lf),
            new(3, string.Empty, null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "结束行缺失时读取到末尾")]
    public void Reads_to_the_end_when_to_is_missing()
    {
        SourceLineReadResult result = Read("一\n二\n", 1, null);

        Assert.True(result.ToBeyondEnd);
        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.Lf),
            new(2, "二", NewLineStyle.Lf),
            new(3, string.Empty, null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "读取带终止符的 CRLF 行")]
    public void Reads_crlf_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.CrLf),
            new(2, "二", NewLineStyle.CrLf),
            new(3, string.Empty, null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "读取带终止符的 CR 行")]
    public void Reads_cr_lines_with_terminators()
    {
        SourceLineReadResult result = Read("一\r二\r", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.Cr),
            new(2, "二", NewLineStyle.Cr),
            new(3, string.Empty, null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "混合文件中保留每行的终止符")]
    public void Keeps_the_terminator_of_each_line_in_a_mixed_file()
    {
        SourceLineReadResult result = Read("一\r\n二\r三\n四", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.CrLf),
            new(2, "二", NewLineStyle.Cr),
            new(3, "三", NewLineStyle.Lf),
            new(4, "四", null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "保留空行")]
    public void Keeps_empty_lines()
    {
        SourceLineReadResult result = Read("一\r\n\r\n二\n", 1, 10);

        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.CrLf),
            new(2, string.Empty, NewLineStyle.CrLf),
            new(3, "二", NewLineStyle.Lf),
            new(4, string.Empty, null),
        ];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "没有终止符的最后一行不带换行")]
    public void Last_line_without_a_terminator_has_no_new_line()
    {
        SourceLineReadResult result = Read("一\n二", 1, 10);

        SourceLine[] expected = [new(1, "一", NewLineStyle.Lf), new(2, "二", null)];
        Assert.Equal(expected, result.Lines);
    }

    [Fact(DisplayName = "末尾终止符增加一个空行")]
    public void Trailing_terminator_adds_an_empty_line()
    {
        SourceLineReadResult result = Read("一\n", 1, 10);

        SourceLine[] expected = [new(1, "一", NewLineStyle.Lf), new(2, string.Empty, null)];
        Assert.Equal(expected, result.Lines);
        Assert.True(result.ToBeyondEnd);
    }

    [Fact(DisplayName = "读取请求的行范围")]
    public void Reads_the_requested_range()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n三\r\n", 2, 3);

        SourceLine[] expected = [new(2, "二", NewLineStyle.CrLf), new(3, "三", NewLineStyle.CrLf)];
        Assert.Equal(expected, result.Lines);
        Assert.False(result.ToBeyondEnd);
    }

    [Fact(DisplayName = "起始行超出末尾时报告")]
    public void Reports_a_start_line_beyond_the_end()
    {
        SourceLineReadResult result = Read("一\r\n", 3, 3);

        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    [Fact(DisplayName = "起始行落在末尾空行时返回该行")]
    public void A_start_line_at_the_trailing_empty_line_returns_it()
    {
        SourceLineReadResult result = Read("一\n", 2, 2);

        Assert.False(result.ToBeyondEnd);
        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(new SourceLine(2, string.Empty, null), line);
    }

    [Fact(DisplayName = "起始行在未终止的最后一行之后视为超出末尾")]
    public void A_start_line_after_an_unterminated_last_line_is_beyond_the_end()
    {
        SourceLineReadResult result = Read("一", 2, 50);

        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    [Fact(DisplayName = "报告文件末尾")]
    public void Reports_a_file_end()
    {
        SourceLineReadResult result = Read("一\n二", 1, 10);

        Assert.True(result.ToBeyondEnd);
    }

    [Fact(DisplayName = "字符预算用尽后停止")]
    public void Stops_after_the_character_budget()
    {
        SourceLineReadResult result = Read("一\r\n二\r\n", 1, 10, characterBudget: 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(new SourceLine(1, "一", NewLineStyle.CrLf), line);
        Assert.False(result.ToBeyondEnd);
    }

    [Fact(DisplayName = "处理跨越缓冲边界的 CRLF")]
    public void Handles_crlf_across_the_buffer_boundary()
    {
        string text = new string('a', 4095) + "\r\nb";

        SourceLineReadResult result = Read(text, 1, 2);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(4095, result.Lines[0].Text.Length);
        Assert.Equal(NewLineStyle.CrLf, result.Lines[0].Terminator);
        Assert.Equal(new SourceLine(2, "b", null), result.Lines[1]);
    }

    [Fact(DisplayName = "处理长于缓冲的行")]
    public void Handles_a_line_longer_than_the_buffer()
    {
        string text = new string('a', 5000) + "\n";

        SourceLineReadResult result = Read(text, 1, 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(5000, line.Text.Length);
        Assert.Equal(NewLineStyle.Lf, line.Terminator);
    }

    [Fact(DisplayName = "跨缓冲保留代理对")]
    public void Preserves_surrogate_pairs_across_the_buffer()
    {
        string content = new string('a', 4095) + "𠀀";
        SourceLineReadResult result = Read($"{content}\n", 1, 1);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(content, line.Text);
        Assert.Equal(NewLineStyle.Lf, line.Terminator);
    }

    [Fact(DisplayName = "读取空文件")]
    public void Reads_an_empty_file()
    {
        SourceLineReadResult result = Read(string.Empty, 1, 10);

        Assert.True(result.ToBeyondEnd);
        Assert.Empty(result.Lines);
    }

    [Fact(DisplayName = "达到上限的行保留终止符")]
    public void Line_at_the_limit_keeps_the_terminator()
    {
        SourceLineReadResult result = Read("abcd\n", 1, 1, lineCharacterBudget: 5);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal(new SourceLine(1, "abcd", NewLineStyle.Lf), line);
        Assert.False(line.Truncated);
        Assert.Equal(5, line.LineLength);
    }

    [Fact(DisplayName = "超过上限的行保留文本前缀并丢弃终止符")]
    public void Line_over_the_limit_keeps_the_text_prefix_and_drops_the_terminator()
    {
        SourceLineReadResult result = Read("abcdef\n", 1, 1, lineCharacterBudget: 5);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal("abcde", line.Text);
        Assert.Null(line.Terminator);
        Assert.True(line.Truncated);
        Assert.Equal(7, line.LineLength);
    }

    [Fact(DisplayName = "仅终止符也可以超过行上限")]
    public void Terminator_alone_can_exceed_the_line_limit()
    {
        SourceLineReadResult result = Read("abcd\r\n", 1, 1, lineCharacterBudget: 5);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal("abcd", line.Text);
        Assert.Null(line.Terminator);
        Assert.True(line.Truncated);
        Assert.Equal(6, line.LineLength);
    }

    [Fact(DisplayName = "行截断不拆分代理对")]
    public void Line_truncation_does_not_split_a_surrogate_pair()
    {
        SourceLineReadResult result = Read("ab𠀀c", 1, 1, lineCharacterBudget: 3);

        SourceLine line = Assert.Single(result.Lines);
        Assert.Equal("ab", line.Text);
        Assert.Equal(5, line.LineLength);
        Assert.True(line.Truncated);
    }

    [Fact(DisplayName = "截断行不消耗字符预算")]
    public void Truncated_line_does_not_consume_the_character_budget()
    {
        SourceLineReadResult result = Read(
            $"{new string('a', 100)}\nnext",
            1,
            2,
            characterBudget: 10,
            lineCharacterBudget: 5);

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal("aaaaa", result.Lines[0].Text);
        Assert.Equal("next", result.Lines[1].Text);
    }

    private static SourceLineReadResult Read(
        string text,
        int from,
        int? to,
        int characterBudget = TextOutputLimit.Characters,
        int lineCharacterBudget = int.MaxValue) =>
        SourceLineReader.Read(new StringReader(text), from, to, characterBudget, lineCharacterBudget);
}