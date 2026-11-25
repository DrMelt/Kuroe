using Xunit;

namespace Kuroe.Host.Ability.Tool.FileAccess.Shared.Tests;

/// <summary>换行风格：归一、识别与转换。</summary>
public sealed class NewLineStyleTests
{
    [Fact(DisplayName = "归一化把 CRLF 与 CR 替换为 LF")]
    public void Normalize_replaces_crlf_and_cr_with_lf()
    {
        Assert.Equal("a\nb\nc", NewLineStyle.Normalize("a\r\nb\rc"));
    }

    [Fact(DisplayName = "没有换行时检测返回 LF")]
    public void Detect_returns_lf_without_a_newline()
    {
        Assert.Equal(NewLineStyle.Lf, NewLineStyle.Detect("abc"));
    }

    [Fact(DisplayName = "检测返回第一个换行风格")]
    public void Detect_returns_the_first_newline_style()
    {
        Assert.Equal(NewLineStyle.CrLf, NewLineStyle.Detect("a\r\nb\nc"));
        Assert.Equal(NewLineStyle.Cr, NewLineStyle.Detect("a\rb\nc"));
        Assert.Equal(NewLineStyle.Lf, NewLineStyle.Detect("a\nb\r\nc"));
    }

    [Fact(DisplayName = "转换把 LF 替换为目标风格")]
    public void Convert_replaces_lf_with_the_style()
    {
        Assert.Equal("a\r\nb\r\n", NewLineStyle.CrLf.Convert("a\nb\n"));
        Assert.Equal("a\rb\r", NewLineStyle.Cr.Convert("a\nb\n"));
    }

    [Fact(DisplayName = "目标为 LF 时转换保持文本")]
    public void Convert_keeps_the_text_for_lf()
    {
        Assert.Equal("a\nb\n", NewLineStyle.Lf.Convert("a\nb\n"));
    }
}