using ErrorOr;

namespace Kuroe.Agent;

/// <summary>按上下文装配会话并发起请求。</summary>
sealed class RunExecutor(AgentSessionFactory sessions) : IRunExecutor
{
    public async Task<ErrorOr<string>> ExecuteAsync(AgentRun run, CancellationToken cancellationToken)
    {
        AgentSession session = sessions.ForRun(run.Context);

        return await session.AskAsync(run.Context.Instruction, run.Scope, cancellationToken);
    }
}

