using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Sessions;

namespace Kuroe.Node.Llm.Executions;

/// <summary>一次执行的运行器：按上下文装配 MAF 会话并持有本次执行的过程记录。
/// 工具调用由 MAF 在同一会话内完成，产出契约未满足时补发要求再请求。</summary>
internal sealed class ExecutionRunner(
    SessionFactory sessions,
    IExecutionStatusReader status,
    ExecutionId execution,
    LlmAgentSessionPayload? restored = null) : IExecutionRunner, IExecutionUsageReader, IExecutionRestoreDataReader
{
    /// <summary>补发要求的上限，用尽仍未交回按收口失败处理。</summary>
    private const int ReminderLimit = 2;

    private readonly SessionFactory _sessions = sessions;
    private readonly ContractReminder _reminder = new(status);
    private readonly ProcessJournal _journal = new();
    private readonly LlmAgentSessionPayload? _restored = restored;
    private volatile IConversation? _session;
    private volatile ExecutionRestorePayload? _restoreData;

    /// <summary>本次执行的过程记录。</summary>
    public IProcessJournal Journal => _journal;

    /// <summary>本次执行的会话用量与最近一次请求的上下文，运行中可读，执行结束后为最终值。</summary>
    public ExecutionUsage Usage => _session?.Usage ?? ExecutionUsage.None;

    /// <summary>本次执行留下的恢复数据，不产生时为空。</summary>
    public ExecutionRestorePayload? RestoreData => _restoreData;

    /// <summary>运行中恢复数据变化后触发。</summary>
    public event Action? RestoreDataChanged;

    /// <summary>执行一次 Llm 请求，按产出契约补发要求。恢复数据存在时从上次中断处继续。</summary>
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionInput input,
        CancellationToken cancellationToken)
    {
        LlmNodeType? type = input.Type as LlmNodeType;
        ErrorOr<IConversation> created = await _sessions.ForExecutionAsync(
            execution,
            input,
            _journal,
            _restored,
            cancellationToken).ConfigureAwait(false);

        if (created.IsError)
        {
            return created.ErrorsOrEmptyList;
        }

        IConversation session = created.Value;
        _session = session;
        if (_restored is not null)
        {
            _journal.AppendRestored();
        }

        try
        {
            ErrorOr<Success> reply = await AskWithReminders(
                text => session.AskAsync(text, cancellationToken),
                session.IsRestored ? null : type?.Instruction(input) ?? string.Empty,
                () => _reminder.Text(execution));

            if (reply.IsError)
            {
                await CaptureInterruptedRound(session).ConfigureAwait(false);

                return reply.ErrorsOrEmptyList;
            }

            await CaptureRestoreData(session, CancellationToken.None).ConfigureAwait(false);

            return ExecutionOutput.Ports;
        }
        catch (Exception)
        {
            await CaptureInterruptedRound(session).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>按会话当前状态留下恢复数据；历史不能继续时丢弃尚未收口的一轮并通知。</summary>
    private async Task CaptureInterruptedRound(IConversation session)
    {
        if (!await CaptureRestoreData(session, CancellationToken.None).ConfigureAwait(false))
        {
            session.DiscardRound();

            return;
        }

        RestoreDataChanged?.Invoke();
    }

    /// <summary>按会话当前状态记下恢复数据，返回是否得到可继续的历史。</summary>
    private async Task<bool> CaptureRestoreData(IConversation session, CancellationToken cancellationToken)
    {
        try
        {
            LlmAgentSessionPayload? state = await session
                .CreateRestoreDataAsync(cancellationToken)
                .ConfigureAwait(false);

            _restoreData = state is null ? null : AgentSessionCodec.Encode(state);

            return state is not null;
        }
        catch (Exception)
        {
            _restoreData = null;

            return false;
        }
    }

    /// <summary>先发本轮指令，随后按产出契约在同一会话里补发要求，最多补发 ReminderLimit 次。</summary>
    internal static async Task<ErrorOr<Success>> AskWithReminders(
        Func<string?, Task<ErrorOr<Success>>> ask,
        string? instruction,
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

