using Kuroe.Flow.Shared.Execution;

namespace Kuroe.Configuration;

/// <summary>执行并发额度取自宿主设置。</summary>
sealed class SettingsExecutionPolicy(SettingsProvider settings) : IExecutionPolicy
{
    public int MaxConcurrentExecutions => settings.Current.Runtime.MaxConcurrentExecutions;
}
