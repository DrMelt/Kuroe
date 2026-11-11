using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Shared.Tests;

/// <summary>文本查找条件：后缀归一与非法条目拒绝，字面与正则模式的建立。</summary>
public sealed class TextQueryTests
{
    [Fact]
    public void Create_normalizes_extensions_and_rejects_invalid_entries()
    {
        ErrorOr<TextQuery> query = TextQuery.Create("x", ["CS", ".md", "cs"]);

        Assert.False(query.IsError);
        Assert.Equal([".cs", ".md"], query.Value.Extensions);
        Assert.True(TextQuery.Create(string.Empty, null).IsError);
        Assert.True(TextQuery.Create("x", [""]).IsError);
        Assert.True(TextQuery.Create("x", ["*.cs"]).IsError);
        Assert.True(TextQuery.Create("x", ["c?.cs"]).IsError);
        Assert.True(TextQuery.Create("x", ["Source/cs"]).IsError);
    }

    [Fact]
    public void CreatePattern_uses_a_literal_pattern_by_default()
    {
        ErrorOr<TextQuery> query = TextQuery.Create("a.c", null);
        Assert.False(query.IsError);

        ErrorOr<ITextPattern> pattern = query.Value.CreatePattern();
        Assert.False(pattern.IsError);

        Assert.False(query.Value.IsRegex);
        Assert.IsType<LiteralTextPattern>(pattern.Value);
        Assert.Equal(new TextMatchSummary(-1, 0), pattern.Value.Scan("abc", 10));
    }

    [Fact]
    public void CreatePattern_uses_a_regex_pattern_when_requested()
    {
        ErrorOr<TextQuery> query = TextQuery.Create("^a+$", null, regex: true);
        Assert.False(query.IsError);

        ErrorOr<ITextPattern> pattern = query.Value.CreatePattern();
        Assert.False(pattern.IsError);

        Assert.True(query.Value.IsRegex);
        Assert.IsType<RegexTextPattern>(pattern.Value);
        Assert.Equal(new TextMatchSummary(0, 1), pattern.Value.Scan("aaa", 10));
    }

    [Fact]
    public void CreatePattern_accepts_a_whitespace_pattern()
    {
        foreach (bool regex in new[] { false, true })
        {
            ErrorOr<TextQuery> query = TextQuery.Create(" ", null, regex);
            Assert.False(query.IsError);

            ErrorOr<ITextPattern> pattern = query.Value.CreatePattern();
            Assert.False(pattern.IsError);
            Assert.Equal(new TextMatchSummary(1, 1), pattern.Value.Scan("a b", 10));
        }
    }

    [Fact]
    public void CreatePattern_rejects_an_invalid_regex()
    {
        ErrorOr<TextQuery> query = TextQuery.Create("(", null, regex: true);
        Assert.False(query.IsError);

        ErrorOr<ITextPattern> pattern = query.Value.CreatePattern();

        Assert.True(pattern.IsError);
        Assert.Contains("pattern", pattern.FirstError.Description);
    }
}