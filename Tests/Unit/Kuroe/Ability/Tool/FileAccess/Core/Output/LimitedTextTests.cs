using Kuroe.Ability.Tool.FileAccess.Output;
using Xunit;

namespace Kuroe.Ability.Tool.FileAccess.Tests.Output;

/// <summary>带字符上限的文本：截断、提示与代理对边界。</summary>
public sealed class LimitedTextTests
{
    [Fact(DisplayName = "限制文本主体保留上限内的内容")]
    public void Body_keeps_content_within_the_limit()
    {
        LimitedText text = new("短文本", TextOutputLimit.Characters);

        Assert.False(text.Truncated);
        Assert.Equal("短文本", text.Body);
        Assert.Equal("短文本", text.ToString());
    }

    [Fact(DisplayName = "限制文本在上限处不截断内容")]
    public void Body_keeps_content_at_the_limit_untruncated()
    {
        LimitedText text = new(new string('a', TextOutputLimit.Characters), TextOutputLimit.Characters);

        Assert.False(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters, text.Body.Length);
        Assert.Equal(text.Body, text.ToString());
    }

    [Fact(DisplayName = "限制文本不留下孤立回车")]
    public void Body_does_not_leave_a_bare_carriage_return()
    {
        LimitedText text = new(
            new string('a', TextOutputLimit.Characters - 1) + "\r\n",
            TextOutputLimit.Characters);

        Assert.True(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters - 1, text.Body.Length);
        Assert.EndsWith("a", text.Body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "限制文本不拆分代理对")]
    public void Body_does_not_split_a_surrogate_pair()
    {
        LimitedText text = new(
            new string('a', TextOutputLimit.Characters - 1) + "𠀀" + new string('b', 10),
            TextOutputLimit.Characters);

        Assert.True(text.Truncated);
        Assert.Equal(TextOutputLimit.Characters - 1, text.Body.Length);
        Assert.EndsWith("a", text.Body, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "限制文本把提示放在主体之前")]
    public void ToString_puts_the_notice_before_the_body()
    {
        string notice = $"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n";

        LimitedText text = new(new string('a', TextOutputLimit.Characters + 1), TextOutputLimit.Characters);
        string content = text.ToString();

        Assert.True(text.Truncated);
        Assert.StartsWith(notice, content, StringComparison.Ordinal);
        Assert.Equal(TextOutputLimit.Characters, content.Length - notice.Length);
    }

    [Fact(DisplayName = "自定义上限控制提示与主体")]
    public void Custom_limit_controls_the_notice_and_body()
    {
        LimitedText text = new("abcdef", 3);

        Assert.True(text.Truncated);
        Assert.Equal("abc", text.Body);
        Assert.Equal("（内容超过 3 字符，已截断）\nabc", text.ToString());
    }
}