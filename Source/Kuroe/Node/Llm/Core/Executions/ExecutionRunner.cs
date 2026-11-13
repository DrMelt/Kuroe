using ErrorOr;
using Kuroe.Flow.Execution.Shared;
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
    ExecutionId execution,
    LlmExecutionParameters parameters,
    LlmExecutionState? restored = null) : IExecutionRunner, IExecutionUsageSource, IExecutionRestoreDataSource
{
    /// <summary>补发要求的上限，用尽仍未交回按收口失败处理。</summary>
    private const int ReminderLimit = 2;

    private readonly SessionFactory _sessions = sessions;
    private readonly ContractReminder _reminder = new(status);
    private readonly ProcessJournal _journal = new();
    private readonly LlmExecutionParameters _parameters = parameters;
    private readonly LlmExecutionState? _restored = restored;
    private volatile Session? _session;
    private volatile ExecutionRestoreState? _restoreData;

    /// <summary>本次执行的过程记录。</summary>
    public IProcessJournal Journal => _journal;

    /// <summary>本次执行的会话用量与最近一次请求的上下文，运行中可读，执行结束后为最终值。</summary>
    public ExecutionUsage Usage => _session?.Usage ?? ExecutionUsage.None;

    /// <summary>本次执行留下的恢复数据，不产生时为空。</summary>
    public ExecutionRestoreState? RestoreData => _restoreData;

    /// <summary>运行中恢复数据变化后触发。</summary>
    public event Action? RestoreDataChanged;

    /// <summary>执行一次 Llm 请求，按产出契约补发要求。恢复数据存在时从上次中断处继续。</summary>
    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        Session session = _sessions.ForExecution(execution, request, _parameters, _journal, _restored);
        _session = session;
        if (_restored is not null)
        {
            _journal.AppendRestored();
        }

        IToolInvoker tools = new ToolInvoker(session.Functions);

        try
        {
            ErrorOr<Success> reply = await AskWithReminders(
                text => TurnLoop(session, tools, text, cancellationToken),
                session.IsRestored ? null : _parameters.Instruction(request),
                () => _reminder.Text(execution));

            if (reply.IsError)
            {
                CaptureInterruptedRound(session);

                return reply.ErrorsOrEmptyList;
            }

            _ = CaptureRestoreData(session);

            return ExecutionOutput.FromPorts([]);
        }
        catch (Exception)
        {
            CaptureInterruptedRound(session);
            throw;
        }
    }

    /// <summary>按会话当前状态留下恢复数据；历史不能继续时丢弃尚未收口的一轮并通知。</summary>
    private void CaptureInterruptedRound(Session session)
    {
        if (!CaptureRestoreData(session))
        {
            session.DiscardRound();

            return;
        }

        RestoreDataChanged?.Invoke();
    }

    /// <summary>按会话当前状态记下恢复数据，返回是否得到可继续的历史。</summary>
    private bool CaptureRestoreData(Session session)
    {
        try
        {
            LlmExecutionState? state = session.CreateRestoreData();
            _restoreData = state is null ? null : LlmExecutionStateCodec.Encode(state);

            return state is not null;
        }
        catch (Exception)
        {
            _restoreData = null;

            return false;
        }
    }

    /// <summary>以一条指令开始，执行模型请求的每次工具调用，直到模型不再请求工具或请求次数用尽。</summary>
    internal static async Task<ErrorOr<Success>> TurnLoop(
        IConversation conversation,
        IToolInvoker tools,
        string? instruction,
        CancellationToken cancellationToken)
    {
        string? input = instruction;
        while (true)
        {
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

