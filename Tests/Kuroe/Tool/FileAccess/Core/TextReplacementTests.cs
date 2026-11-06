using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

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
    public void Create_accepts_identical_text_when_ignoring_case()
    {
        TextReplacement replacement = Success(TextReplacement.Create(Query("needle", ignoreCase: true), "needle"));

        Assert.Equal("needle", replacement.Replacement);
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

        Assert.Equal("a\nb\nc", replacement.Pattern);
        Assert.Equal("x\ny\nz", replacement.Replacement);
    }

    private static TextQuery Query(string pattern, bool ignoreCase = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, null, ignoreCase);
        Assert.False(query.IsError);

        return query.Value;
    }

    private static TextReplacement Success(ErrorOr<TextReplacement> result)
    {
        Assert.False(result.IsError);

        return result.Value;
    }
}