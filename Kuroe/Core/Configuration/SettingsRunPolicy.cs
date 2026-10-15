using Kuroe.Flow.Shared.Execution.Runs;

namespace Kuroe.Configuration;

/// <summary>run 并发额度取自宿主设置。</summary>
sealed class SettingsRunPolicy(SettingsProvider settings) : IRunPolicy
{
    public int MaxConcurrentRuns => settings.Current.Runtime.MaxConcurrentRuns;
}
