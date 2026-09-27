using ErrorOr;
using Kuroe.Executions.Sessions;

namespace Kuroe.Executions.Runs;

/// <summary>按上下文装配会话并发起请求。</summary>
sealed class RunExecutor(SessionFactory sessions) : IRunExecutor
{
    public async Task<ErrorOr<string>> ExecuteAsync(Run run, CancellationToken cancellationToken)
    {
        Session session = sessions.ForRun(run.Context);

        return await session.AskAsync(run.Context.Instruction, run.Scope, cancellationToken);
    }
}

