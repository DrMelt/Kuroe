using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的运行器：按上下文装配会话并持有本次执行的过程记录。
/// 每轮请求都带完整工具面，模型请求工具时在同一会话内执行并回填结果，直到模型给出回复。
/// 产出契约未满足时在同一会话里补发要求再请求，模型因此能看到自己上一轮的回复。
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
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken)
    {
        LlmNodeType? type = input.Type as LlmNodeType;
        Session session = _sessions.ForExecution(execution, input, _journal);
        IToolInvoker tools = new ToolInvoker(session.Functions);
        TurnBudget? budget = type?.MaxTurns is { } maxTurns ? new TurnBudget(maxTurns) : null;

        ErrorOr<Success> reply = await AskWithReminders(
            text => TurnLoop(session, tools, budget, text, cancellationToken),
            type?.Instruction(input) ?? string.Empty,
            () => _reminder.Text(execution));

        return reply.IsError ? reply.ErrorsOrEmptyList : ExecutionOutput.Ports;
    }

    /// <summary>以一条指令开始，执行模型请求的每次工具调用，直到模型不再请求工具或请求次数用尽。</summary>
    internal static async Task<ErrorOr<Success>> TurnLoop(
        IConversation conversation,
        IToolInvoker tools,
        TurnBudget? budget,
        string instruction,
        CancellationToken cancellationToken)
    {
        string? input = instruction;
        while (true)
        {
            if (budget is { } limited && !limited.Take())
            {
                return [ModelErrors.TurnLimit(limited.Limit)];
            }

            ErrorOr<TurnReply> reply = await conversation.AskAsync(input, cancellationToken);
            if (reply.IsError)
            {
                return reply.ErrorsOrEmptyList;
            }

            if (reply.Value.Calls.Count == 0)
            {
                return Result.Success;
            }

            IReadOnlyList<FunctionResultContent> results = await tools.InvokeAsync(reply.Value.Calls, cancellationToken);
            conversation.AppendResults(results);
            input = null;
        }
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

