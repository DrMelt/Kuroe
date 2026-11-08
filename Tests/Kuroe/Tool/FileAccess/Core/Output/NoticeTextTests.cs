using Kuroe.Tool.FileAccess.Output;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests.Output;

/// <summary>带前缀提示的文本：提示与正文的拼接。</summary>
public sealed class NoticeTextTests
{
    [Fact]
    public void ToString_separates_notice_and_body_with_lf()
    {
        NoticeText text = new("提示", "正文");

        Assert.Equal("提示\n正文", text.ToString());
    }

    [Fact]
    public void ToString_returns_body_without_notice()
    {
        NoticeText text = new(string.Empty, "正文");

        Assert.Equal("正文", text.ToString());
    }
}