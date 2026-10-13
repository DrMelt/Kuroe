namespace Kuroe.Flows.Shared.Execution.Runs;

/// <summary>run 的并发策略，由宿主给出。</summary>
public interface IRunPolicy
{
    /// <summary>同时执行的 run 数上限。</summary>
    int MaxConcurrentRuns { get; }
}
