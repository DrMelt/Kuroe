using ErrorOr;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的运行器：按上下文装配会话并持有本次执行的过程记录。产出契约未满足时在同一会话里补发要求再请求，模型因此能看到自己上一轮的回复。
/// 请求的模型侧内容全部取自节点类型自身给出的指令、系统指令与工具面，因此只适用于 Llm 类型的执行节点。</summary>
internal sealed class ExecutionRunner(
    SessionFactory sessions,
    IExecutionStatusReader status,
    ExecutionId execution) : IExecutionRunner
{
    /// <summary>补发要求的上限，用尽仍未交回按收口失败处理。</summary>
    private const int ReminderLimit = 2;

    private readonly SessionFactory _sessions = sessions;
    private readonly ContractReminder _reminder = new(status);
    private readonly ProcessJournal _journal = new();

    /// <summary>本次执行的过程记录。</summary>
    public IProcessJournal Journal => _journal;

    /// <summary>执行一次 Llm 请求，按产出契约补发要求。</summary>
    public async Task<ErrorOr<Success>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken)
    {
        Session session = _sessions.ForExecution(input);

        return await AskWithReminders(
            text => session.AskAsync(text, execution, _journal, cancellationToken),
            (input.Type as LlmNodeType)?.Instruction(input) ?? string.Empty,
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

