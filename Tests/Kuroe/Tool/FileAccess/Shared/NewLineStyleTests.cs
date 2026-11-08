using Xunit;

namespace Kuroe.Tool.FileAccess.Shared.Tests;

/// <summary>换行风格：归一、识别与转换。</summary>
public sealed class NewLineStyleTests
{
    [Fact]
    public void Normalize_replaces_crlf_and_cr_with_lf()
    {
        Assert.Equal("a\nb\nc", NewLineStyle.Normalize("a\r\nb\rc"));
    }

    [Fact]
    public void Detect_returns_lf_without_a_newline()
    {
        Assert.Equal(NewLineStyle.Lf, NewLineStyle.Detect("abc"));
    }

    [Fact]
    public void Detect_returns_the_first_newline_style()
    {
        Assert.Equal(NewLineStyle.CrLf, NewLineStyle.Detect("a\r\nb\nc"));
        Assert.Equal(NewLineStyle.Cr, NewLineStyle.Detect("a\rb\nc"));
        Assert.Equal(NewLineStyle.Lf, NewLineStyle.Detect("a\nb\r\nc"));
    }

    [Fact]
    public void Convert_replaces_lf_with_the_style()
    {
        Assert.Equal("a\r\nb\r\n", NewLineStyle.CrLf.Convert("a\nb\n"));
        Assert.Equal("a\rb\r", NewLineStyle.Cr.Convert("a\nb\n"));
    }

    [Fact]
    public void Convert_keeps_the_text_for_lf()
    {
        Assert.Equal("a\nb\n", NewLineStyle.Lf.Convert("a\nb\n"));
    }
}