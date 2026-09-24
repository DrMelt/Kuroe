using ErrorOr;

namespace Kuroe.Agent.Runs;

/// <summary>执行一个 agent 的一轮请求。替换它即可在不接模型的情况下验证流程推进。</summary>
public interface IRunExecutor
{
    /// <summary>执行一轮请求，返回完整回复或错误。</summary>
    Task<ErrorOr<string>> ExecuteAsync(AgentRun run, CancellationToken cancellationToken);
}

