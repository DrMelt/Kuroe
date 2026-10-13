using ErrorOr;
using Kuroe.Executions.Sessions;
using Kuroe.Flows.Shared.Execution.Runs;

namespace Kuroe.Executions.Runs;

/// <summary>按上下文装配会话并发起请求。产出契约未满足时在同一会话里补发要求再请求，模型因此能看到自己上一轮的回复。</summary>
sealed class RunExecutor(SessionFactory sessions, IRunContract contract) : IRunExecutor
{
    /// <summary>补发要求的上限，用尽仍未交回按收口失败处理。</summary>
    private const int ReminderLimit = 2;

    public Task<ErrorOr<string>> ExecuteAsync(IRun run, CancellationToken cancellationToken)
    {
        Session session = sessions.ForRun(run.Context);

        return AskWithReminders(
            text => session.AskAsync(text, run.Scope, cancellationToken),
            run.Context.Instruction,
            () => contract.Reminder(run));
    }

    /// <summary>先发本轮指令，随后按产出契约在同一会话里补发要求，最多补发 ReminderLimit 次。</summary>
    internal static async Task<ErrorOr<string>> AskWithReminders(
        Func<string, Task<ErrorOr<string>>> ask,
        string instruction,
        Func<string?> reminder)
    {
        ErrorOr<string> reply = await ask(instruction);
        for (int sent = 0; sent < ReminderLimit && !reply.IsError; sent++)
        {
            if (reminder() is not { } text)
            {
                break;
            }

            reply = await ask(text);
        }

        return reply;
    }
}

