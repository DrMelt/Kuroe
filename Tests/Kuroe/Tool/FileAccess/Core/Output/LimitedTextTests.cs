using Kuroe.Tool.FileAccess.Output;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests.Output;

/// <summary>带字符上限的文本：截断、提示与代理对边界。</summary>
public sealed class LimitedTextTests
{
    [Fact]
    public void Body_keeps_content_within_the_limit()
    {
        LimitedText text = new("短文本");

        Assert.False(text.Truncated);
        Assert.Equal("短文本", text.Body);
        Assert.Equal("短文本", text.ToString());
    }

    [Fact]
    public void Body_keeps_content_at_the_limit_untruncated()
    {
        LimitedText text = new(new string('a', TextOutputLimit.Characters));

        Assert.False(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters, text.Body.Length);
        Assert.Equal(text.Body, text.ToString());
    }

    [Fact]
    public void Body_does_not_leave_a_bare_carriage_return()
    {
        LimitedText text = new(new string('a', TextOutputLimit.Characters - 1) + "\r\n");

        Assert.True(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters - 1, text.Body.Length);
        Assert.EndsWith("a", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Body_does_not_split_a_surrogate_pair()
    {
        LimitedText text = new(new string('a', TextOutputLimit.Characters - 1) + "𠀀" + new string('b', 10));

        Assert.True(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters - 1, text.Body.Length);
        Assert.EndsWith("a", text.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_puts_the_notice_before_the_body()
    {
        string notice = $"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n";

        LimitedText text = new(new string('a', TextOutputLimit.Characters + 1));
        string content = text.ToString();

        Assert.True(text.Truncated);
        Assert.StartsWith(notice, content, StringComparison.Ordinal);
        Assert.Equal(TextOutputLimit.Characters, content.Length - notice.Length);
    }
}