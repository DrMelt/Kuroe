using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Shared.Tests;

/// <summary>文本替换条件：缺参数、空 pattern、区分大小写时的相同文本与换行归一。</summary>
public sealed class TextReplacementTests
{
    [Fact]
    public void Create_rejects_missing_replacement()
    {
        ErrorOr<TextReplacement> result = TextReplacement.Create(Query("old"), null);

        Assert.True(result.IsError);
        Assert.Contains("replacement", result.FirstError.Description);
    }

    [Fact]
    public void Create_rejects_identical_text()
    {
        ErrorOr<TextReplacement> result = TextReplacement.Create(Query("old"), "old");

        Assert.True(result.IsError);
        Assert.Contains("相同", result.FirstError.Description);
    }

    [Fact]
    public void Create_rejects_identical_text_after_newline_normalization()
    {
        ErrorOr<TextReplacement> result = TextReplacement.Create(Query("a\r\nb"), "a\nb");

        Assert.True(result.IsError);
    }

    [Fact]
    public void Create_accepts_empty_replacement()
    {
        TextReplacement replacement = Success(TextReplacement.Create(Query("old"), string.Empty));

        Assert.Equal(string.Empty, replacement.Replacement);
    }

    [Fact]
    public void Create_normalizes_pattern_and_replacement_newlines()
    {
        TextReplacement replacement = Success(TextReplacement.Create(Query("a\r\nb\rc"), "x\r\ny\nz"));

        Assert.Equal("a\nb\nc", replacement.Pattern.Source);
        Assert.Equal("x\ny\nz", replacement.Replacement);
    }

    [Fact]
    public void Create_accepts_identical_text_for_a_regex()
    {
        TextReplacement replacement = Success(TextReplacement.Create(Query("a+", regex: true), "a+"));

        Assert.Equal("a+", replacement.Replacement);
    }

    [Fact]
    public void Create_accepts_a_whitespace_pattern()
    {
        TextReplacement literal = Success(TextReplacement.Create(Query(" "), "x"));
        TextReplacement regex = Success(TextReplacement.Create(Query(" ", regex: true), "x"));

        Assert.Equal(" ", literal.Pattern.Source);
        Assert.Equal(" ", regex.Pattern.Source);
    }

    [Fact]
    public void Create_accepts_an_unknown_group_reference_in_the_template()
    {
        TextReplacement replacement =
            Success(TextReplacement.Create(Query("(?<word>[a-z]+)", regex: true), "${nope}"));

        Assert.Equal("${nope}", replacement.Replacement);
    }

    private static TextQuery Query(string pattern, bool regex = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, null, regex);
        Assert.False(query.IsError);

        return query.Value;
    }

    private static TextReplacement Success(ErrorOr<TextReplacement> result)
    {
        Assert.False(result.IsError);

        return result.Value;
    }
}