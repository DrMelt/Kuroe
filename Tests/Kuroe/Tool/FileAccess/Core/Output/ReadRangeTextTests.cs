using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests.Output;

/// <summary>读取输出：行号、终止符、截断、范围标记与文件尾提示。</summary>
public sealed class ReadRangeTextTests
{
    [Fact]
    public void ToString_numbers_lines_and_marks_the_file_end()
    {
        SourceLine[] lines =
        [
            new(2, "二", NewLineStyle.Lf),
            new(3, "三", NewLineStyle.Lf),
            new(4, "四", NewLineStyle.Lf),
            new(5, string.Empty, null),
        ];
        SourceLineReadResult read = new(lines, ToBeyondEnd: true);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("（已到文件结尾，实际返回第 2 到 5 行）\n<<<KUROE_TEXT_BEGIN>>>2 |二\n3 |三\n4 |四\n5 |<<<KUROE_TEXT_END>>>", text);
    }

    [Fact]
    public void ToString_can_omit_line_numbers()
    {
        SourceLine[] lines =
        [
            new(1, "一", NewLineStyle.CrLf),
            new(2, "二", NewLineStyle.Cr),
            new(3, "三", NewLineStyle.Lf),
            new(4, "四", null),
        ];
        SourceLineReadResult read = new(lines, ToBeyondEnd: true);

        string text = new ReadRangeText(read, LineNumbers: false).ToString();

        Assert.Equal("（已到文件结尾，实际返回第 1 到 4 行）\n<<<KUROE_TEXT_BEGIN>>>一\r\n二\r三\n四<<<KUROE_TEXT_END>>>", text);
    }

    [Fact]
    public void ToString_aligns_line_numbers_to_the_last_line()
    {
        SourceLine[] lines =
        [
            new(8, "L8", NewLineStyle.Lf),
            new(9, "L9", NewLineStyle.Lf),
            new(10, "L10", NewLineStyle.Lf),
            new(11, "L11", NewLineStyle.Lf),
            new(12, "L12", NewLineStyle.Lf),
        ];
        SourceLineReadResult read = new(lines, ToBeyondEnd: false);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>> 8 |L8\n 9 |L9\n10 |L10\n11 |L11\n12 |L12\n<<<KUROE_TEXT_END>>>", text);
    }

    [Fact]
    public void ToString_keeps_the_terminator_of_the_last_line()
    {
        SourceLine[] lines = [new(1, "一", NewLineStyle.CrLf), new(2, "二", NewLineStyle.CrLf)];
        SourceLineReadResult read = new(lines, ToBeyondEnd: false);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>1 |一\r\n2 |二\r\n<<<KUROE_TEXT_END>>>", text);
    }

    [Fact]
    public void ToString_places_the_end_marker_after_a_cr_terminator()
    {
        SourceLine[] lines = [new(1, "一", NewLineStyle.Cr), new(2, "二", NewLineStyle.Cr)];
        SourceLineReadResult read = new(lines, ToBeyondEnd: false);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>1 |一\r2 |二\r<<<KUROE_TEXT_END>>>", text);
    }

    [Fact]
    public void ToString_marks_truncation_before_the_content()
    {
        SourceLine[] lines = [new(1, new string('a', 9000), null)];
        SourceLineReadResult read = new(lines, ToBeyondEnd: false);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        string notice = $"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n";
        const string Begin = "<<<KUROE_TEXT_BEGIN>>>";
        const string End = "<<<KUROE_TEXT_END>>>";
        Assert.StartsWith(notice + Begin, text, StringComparison.Ordinal);
        Assert.EndsWith(End, text, StringComparison.Ordinal);
        string content = text[(notice.Length + Begin.Length)..^End.Length];
        Assert.Equal(TextOutputLimit.Characters, content.Length);
        Assert.StartsWith("1 |", content, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_reports_truncation_instead_of_the_file_end()
    {
        SourceLine[] lines = [new(1, new string('a', TextOutputLimit.Characters + 1000), null)];
        SourceLineReadResult read = new(lines, ToBeyondEnd: true);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.StartsWith(
            $"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n<<<KUROE_TEXT_BEGIN>>>",
            text,
            StringComparison.Ordinal);
        Assert.DoesNotContain("已到文件结尾", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_reports_an_empty_file()
    {
        SourceLineReadResult read = new([], ToBeyondEnd: true);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("（已到文件结尾，文件没有内容）", text);
    }

    [Fact]
    public void ToString_omits_the_end_notice_within_the_file()
    {
        SourceLine[] lines = [new(1, "一", null)];
        SourceLineReadResult read = new(lines, ToBeyondEnd: false);

        string text = new ReadRangeText(read, LineNumbers: true).ToString();

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>1 |一<<<KUROE_TEXT_END>>>", text);
    }
}