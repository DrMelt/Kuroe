using ErrorOr;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Node.Llm.Executions;

/// <summary>按上下文装配会话并创建本次执行的过程记录。产出契约未满足时在同一会话里补发要求再请求，模型因此能看到自己上一轮的回复。
/// 请求的模型侧内容全部取自节点类型自身给出的指令、系统指令与工具面，因此只适用于 Llm 类型的执行节点。</summary>
public sealed class ExecutionRunner(SessionFactory sessions, IExecutionStatusReader status) : IExecutionRunner
{
    /// <summary>补发要求的上限，用尽仍未交回按收口失败处理。</summary>
    private const int ReminderLimit = 2;

    private readonly SessionFactory _sessions = sessions;
    private readonly ContractReminder _reminder = new(status);

    /// <summary>为一次执行创建会话与过程记录，两者都随执行体走。</summary>
    public IExecutionBody CreateBody(ExecutionInput input) =>
        new LlmExecution(_sessions.ForExecution(input), new ProcessJournal());

    /// <summary>执行一次 Llm 请求，按产出契约补发要求。</summary>
    public Task<ErrorOr<Success>> ExecuteAsync(IExecution execution, IExecutionBody body, CancellationToken cancellationToken)
    {
        LlmExecution current = (LlmExecution)body;

        return AskWithReminders(
            text => current.Session.AskAsync(text, execution, current.Journal, cancellationToken),
            (execution.Input.Type as LlmNodeType)?.Instruction(execution.Input) ?? string.Empty,
            () => _reminder.Text(execution));
    }

    /// <summary>先发本轮指令，随后按产出契约在同一会话里补发要求，最多补发 ReminderLimit 次。</summary>
    internal static async Task<ErrorOr<Success>> AskWithReminders(
        Func<string, Task<ErrorOr<Success>>> ask,
        string instruction,
        Func<string?> reminder)
    {
        ErrorOr<Success> reply = await ask(instruction);
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

