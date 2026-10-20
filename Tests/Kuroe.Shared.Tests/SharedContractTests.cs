using Kuroe.Shared.Configuration;
using Xunit;

namespace Kuroe.Shared.Tests;

/// <summary>宿主侧的契约模型：偏好改动的影响标记。</summary>
public sealed class SharedContractTests
{
    [Fact]
    public void SettingsEffect_none_means_no_change()
    {
        Assert.False(SettingsEffect.None.RequiresRestart);
        Assert.False(SettingsEffect.None.InvalidatesHistory);
        Assert.False(SettingsEffect.None.RequiresDialogueRebuild);
    }
}
