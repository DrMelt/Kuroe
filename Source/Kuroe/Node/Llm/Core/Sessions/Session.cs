using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一段连续的模型对话。历史由 MAF 会话与历史提供者承载，来源决定模型、系统指令与工具面。</summary>
internal sealed class Session(
    ChatClientAgent agent,
    AgentSession session,
    InMemoryChatHistoryProvider history,
    ISessionHistory source,
    ProcessJournal journal,
    ExecutionChatClient client,
    bool restored) : IConversation
{
    private readonly ChatClientAgent _agent = agent;
    private readonly AgentSession _session = session;
    private readonly InMemoryChatHistoryProvider _history = history;
    private readonly ISessionHistory _source = source;
    private readonly ProcessJournal _journal = journal;
    private readonly ExecutionChatClient _client = client;
    private readonly bool _isRestored = restored;
    private int _roundStart;

    /// <summary>本次对话的模型请求用量与最近一次请求的上下文。</summary>
    public ExecutionUsage Usage => _client.Usage;

    /// <summary>本次执行是否从恢复数据开始。</summary>
    public bool IsRestored => _isRestored;

    /// <summary>发起一轮请求，input 不为 null 时作为本轮输入写入历史。工具调用由 MAF 在同一会话内完成。
    /// 输入先写入历史再请求，请求失败时本轮输入仍留在恢复数据里。</summary>
    public async Task<ErrorOr<Success>> AskAsync(string? input, CancellationToken cancellationToken)
    {
        List<ChatMessage> messages = _history.GetMessages(_session);
        _roundStart = messages.Count;

        if (input is not null)
        {
            _journal.AppendPrompt(input);
            messages.Add(new ChatMessage(ChatRole.User, input));
        }

        try
        {
            await _agent.RunAsync(_session, null, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error.Failure("Llm.Request", $"模型请求失败：{ex.Message}");
        }

        NormalizeHistory();

        return Result.Success;
    }

    /// <summary>生成本次执行可恢复的数据，历史末尾不合法时为空。</summary>
    public async Task<LlmAgentSessionPayload?> CreateRestoreDataAsync(CancellationToken cancellationToken)
    {
        if (_source.Model is not { } model)
        {
            return null;
        }

        List<ChatMessage> messages = _history.GetMessages(_session);
        if (!AgentSessionCodec.CanContinue(messages))
        {
            return null;
        }

        return await AgentSessionCodec
            .CaptureAsync(_agent, _session, model.Value, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>丢弃尚未收口的一轮，回到本轮开始前的历史。</summary>
    public void DiscardRound()
    {
        List<ChatMessage> messages = _history.GetMessages(_session);
        if (messages.Count > _roundStart)
        {
            messages.RemoveRange(_roundStart, messages.Count - _roundStart);
            _history.SetMessages(_session, messages);
        }
    }

    /// <summary>规范本轮消息：过滤用量与空内容，合并连续助手消息，空回复整轮丢弃。</summary>
    private void NormalizeHistory()
    {
        List<ChatMessage> messages = _history.GetMessages(_session);
        List<ChatMessage> normalized = [];
        foreach (ChatMessage message in messages)
        {
            List<AIContent> contents = [.. message.Contents.Where(content => content is not UsageContent)];
            if (contents.Count == 0)
            {
                continue;
            }

            ChatMessage current = new(message.Role, contents);
            if (normalized.Count > 0
                && current.Role == ChatRole.Assistant
                && normalized[^1].Role == ChatRole.Assistant)
            {
                ChatMessage previous = normalized[^1];
                normalized[^1] = new ChatMessage(ChatRole.Assistant, [.. previous.Contents, .. current.Contents]);
            }
            else
            {
                normalized.Add(current);
            }
        }

        int roundStart = Math.Min(_roundStart, normalized.Count);
        bool hasReply = normalized
            .Skip(roundStart)
            .Any(message => message.Role == ChatRole.Assistant
                && message.Contents.Any(content => content is TextContent or TextReasoningContent));

        if (!hasReply && !_isRestored)
        {
            if (normalized.Count > roundStart)
            {
                normalized.RemoveRange(roundStart, normalized.Count - roundStart);
            }

            _journal.AppendDiscarded();
        }

        _history.SetMessages(_session, normalized);
    }
}

