using ErrorOr;
using Xunit;

namespace Kuroe.Host.Ability.Tool.FileAccess.Shared.Tests;

/// <summary>文本查找模式：字面与正则的匹配统计、大小写、锚点、零宽匹配与替换模板展开。</summary>
public sealed class TextPatternTests
{
    [Fact(DisplayName = "字面扫描不解释元字符")]
    public void Literal_scan_does_not_interpret_meta_characters()
    {
        LiteralTextPattern pattern = Literal("a.c");

        Assert.Equal(new TextMatchSummary(0, 1), pattern.Scan("a.c", 10));
        Assert.Equal(new TextMatchSummary(-1, 0), pattern.Scan("abc", 10));
    }

    [Fact(DisplayName = "字面扫描统计不重叠的匹配")]
    public void Literal_scan_counts_non_overlapping_occurrences()
    {
        Assert.Equal(new TextMatchSummary(0, 2), Literal("aa").Scan("aaaa", 10));
        Assert.Equal(new TextMatchSummary(0, 1), Literal("aa").Scan("aaaa", 1));
        Assert.Equal(new TextMatchSummary(1, 1), Literal("aa").Scan("baa", 10));
    }

    [Fact(DisplayName = "字面扫描区分大小写")]
    public void Literal_scan_is_case_sensitive()
    {
        Assert.Equal(new TextMatchSummary(-1, 0), Literal("needle").Scan("Needle", 10));
    }

    [Fact(DisplayName = "字面替换原样保留模板")]
    public void Literal_replace_keeps_the_template_verbatim()
    {
        LiteralTextPattern pattern = Literal("a");

        TextMatchReplacement replacement = Assert.Single(pattern.Replace("a", "$1"));

        Assert.Equal(new TextMatchReplacement(0, 1, "$1"), replacement);
    }

    [Fact(DisplayName = "正则扫描匹配模式与锚点")]
    public void Regex_scan_matches_patterns_and_anchors()
    {
        Assert.Equal(new TextMatchSummary(1, 1), RegexPattern(@"\d+").Scan("L22", 10));
        Assert.Equal(new TextMatchSummary(0, 1), RegexPattern("^a+$").Scan("aaa", 10));
        Assert.Equal(new TextMatchSummary(-1, 0), RegexPattern("^a+$").Scan("baaa", 10));
    }

    [Fact(DisplayName = "正则扫描统计零宽匹配")]
    public void Regex_scan_counts_zero_width_matches()
    {
        Assert.Equal(new TextMatchSummary(0, 3), RegexPattern("a*").Scan("ba", 10));
        Assert.Equal(new TextMatchSummary(0, 2), RegexPattern("a*").Scan("ba", 2));
    }

    [Fact(DisplayName = "正则扫描遵循内联大小写选项")]
    public void Regex_scan_honors_inline_case_options()
    {
        Assert.Equal(new TextMatchSummary(-1, 0), RegexPattern("needle").Scan("Needle", 10));
        Assert.Equal(new TextMatchSummary(0, 2), RegexPattern("(?i)needle").Scan("Needle needle", 10));
    }

    [Fact(DisplayName = "正则替换展开分组引用")]
    public void Regex_replace_expands_group_references()
    {
        TextMatchReplacement replacement =
            Assert.Single(RegexPattern(@"([a-z]+)(\d+)").Replace("abc123", "$2-$1"));

        Assert.Equal(0, replacement.Index);
        Assert.Equal(6, replacement.Length);
        Assert.Equal("123-abc", replacement.Text);
    }

    [Fact(DisplayName = "正则替换保留未知分组引用为字面量")]
    public void Regex_replace_keeps_unknown_group_references_literal()
    {
        TextMatchReplacement replacement =
            Assert.Single(RegexPattern(@"(?<word>[a-z]+)").Replace("abc", "${nope}"));

        Assert.Equal("${nope}", replacement.Text);
    }

    [Fact(DisplayName = "上限非正时扫描报告没有匹配")]
    public void Scan_reports_no_match_when_the_limit_is_not_positive()
    {
        Assert.Equal(new TextMatchSummary(-1, 0), Literal("a").Scan("a", 0));
        Assert.Equal(new TextMatchSummary(-1, 0), RegexPattern("a").Scan("a", 0));
    }

    [Fact(DisplayName = "正则替换展开整体匹配与转义美元符号")]
    public void Regex_replace_expands_whole_match_and_escaped_dollar()
    {
        TextMatchReplacement replacement = Assert.Single(RegexPattern("[a-z]+").Replace("abc", "<$&$$>"));

        Assert.Equal("<abc$>", replacement.Text);
    }

    [Fact(DisplayName = "正则创建拒绝非法模式与不支持的构造")]
    public void Regex_create_rejects_invalid_patterns_and_unsupported_constructs()
    {
        Assert.True(RegexTextPattern.Create("(").IsError);
        Assert.True(RegexTextPattern.Create("(?=a)").IsError);
    }

    private static LiteralTextPattern Literal(string source) =>
        LiteralTextPattern.Create(source);

    private static RegexTextPattern RegexPattern(string source)
    {
        ErrorOr<RegexTextPattern> pattern = RegexTextPattern.Create(source);
        Assert.False(pattern.IsError);

        return pattern.Value;
    }
}