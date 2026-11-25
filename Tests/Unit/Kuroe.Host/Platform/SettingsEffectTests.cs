using Kuroe.Host.Platform.Configuration;
using Xunit;

namespace Kuroe.Host.Platform.Tests;

/// <summary>平台的契约模型：偏好改动的影响标记。</summary>
public sealed class SettingsEffectTests
{
    [Fact(DisplayName = "配置效果为无表示不改变")]
    public void SettingsEffect_none_means_no_change()
    {
        Assert.False(SettingsEffect.None.RequiresRestart);
        Assert.False(SettingsEffect.None.InvalidatesHistory);
        Assert.False(SettingsEffect.None.AffectsNewDialogueSessions);
    }
}
