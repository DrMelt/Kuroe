using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>工具输出的字符上限与截断。</summary>
public sealed class TextLimitTests
{
    [Fact]
    public void Body_keeps_content_within_the_limit()
    {
        string body = TextLimit.Body("短文本", out bool truncated);

        Assert.False(truncated);
        Assert.Equal("短文本", body);
    }

    [Fact]
    public void Body_does_not_leave_a_bare_carriage_return()
    {
        string body = TextLimit.Body(new string('a', 7999) + "\r\n", out bool truncated);

        Assert.True(truncated);
        Assert.Equal(7999, body.Length);
        Assert.EndsWith("a", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Body_does_not_split_a_surrogate_pair()
    {
        string content = new string('a', 7999) + "𠀀" + new string('b', 10);

        string body = TextLimit.Body(content, out bool truncated);

        Assert.True(truncated);
        Assert.Equal(7999, body.Length);
        Assert.EndsWith("a", body, StringComparison.Ordinal);
    }

    [Fact]
    public void WithNotice_puts_the_notice_before_the_body()
    {
        const string Notice = "（内容超过 8000 字符，已截断）\n";

        string content = TextLimit.WithNotice(new string('a', 8001), out bool truncated);

        Assert.True(truncated);
        Assert.StartsWith(Notice, content, StringComparison.Ordinal);
        Assert.Equal(8000, content.Length - Notice.Length);
    }
}