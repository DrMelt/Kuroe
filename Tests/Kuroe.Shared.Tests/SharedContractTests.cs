using Kuroe.Shared.Configuration;
using Kuroe.Shared.Workflows;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>宿主侧的契约模型：偏好改动的影响标记与对话回复文本。</summary>
public sealed class SharedContractTests
{
    [Fact]
    public void SettingsEffect_none_means_no_change()
    {
        Assert.False(SettingsEffect.None.RequiresRestart);
        Assert.False(SettingsEffect.None.InvalidatesHistory);
    }

    [Fact]
    public void DialogueReply_carries_the_reply_text()
    {
        DialogueReply reply = new("好的");

        Assert.Equal("好的", reply.Text);
    }
}
