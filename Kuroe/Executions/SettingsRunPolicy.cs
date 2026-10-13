using Kuroe.Configuration;
using Kuroe.Flows.Execution.Runs;
using Kuroe.Flows.Shared.Execution.Runs;

namespace Kuroe.Executions;

/// <summary>run 并发额度取自宿主设置。</summary>
sealed class SettingsRunPolicy(SettingsProvider settings) : IRunPolicy
{
    public int MaxConcurrentRuns => settings.Current.Runtime.MaxConcurrentRuns;
}
