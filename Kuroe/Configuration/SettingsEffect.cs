namespace Kuroe.Configuration;

/// <summary>一次配置改动对运行态的影响。两项都为假表示改动即时生效且不影响上下文。</summary>
public sealed record SettingsEffect(bool RequiresRestart, bool InvalidatesHistory)
{
    /// <summary>没有改动，或改动不触及任何生效条件。</summary>
    public static SettingsEffect None { get; } = new(false, false);
}
