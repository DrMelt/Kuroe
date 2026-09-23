using ErrorOr;

namespace Kuroe.Agent;

/// <summary>执行一个 agent 的一轮请求。替换它即可在不接模型的情况下验证流程推进。</summary>
public interface IRunExecutor
{
    Task<ErrorOr<string>> ExecuteAsync(AgentRun run, CancellationToken cancellationToken);
}

