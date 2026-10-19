namespace Kuroe.Flow.Shared.Execution;

/// <summary>执行的并发策略，由宿主给出。</summary>
public interface IExecutionPolicy
{
    /// <summary>同时在跑的执行数上限。</summary>
    int MaxConcurrentExecutions { get; }
}
