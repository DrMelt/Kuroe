using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一段连续的模型对话。历史由会话持有，来源由 <see cref="ISessionHistory"/> 决定。
/// 每轮发送完整历史与工具面，工具调用由调用方执行后作为结果写回。</summary>
internal sealed class Session : IConversation
{
    private readonly ITurnClientSource _clients;
    private readonly ISessionHistory _source;
    private readonly ProcessJournal _journal;
    private readonly List<ChatMessage> _messages = [];
    private readonly IReadOnlyList<AIFunction> _functions;
    private volatile ExecutionUsage _usage = ExecutionUsage.None;
    private readonly bool _isRestored;
    private int _roundStart;
    private bool _roundOpen;
    private int _restoredIndex = -1;

    internal Session(
        ITurnClientSource clients,
        ToolCollection tools,
        ISessionHistory source,
        ProcessJournal journal,
        IReadOnlyList<ChatMessage>? restored = null)
    {
        _clients = clients;
        _source = source;
        _journal = journal;
        _functions = tools.Build(journal, source.ToolPaths);

        if (restored is null)
        {
            source.LayOut(_messages);
        }
        else
        {
            _isRestored = true;
            _messages.AddRange(restored);
            _restoredIndex = _messages.Count > 0 && IsRestorableReply(_messages[^1])
                ? _messages.Count - 1
                : -1;
        }
    }

    /// <summary>本轮可用的工具函数，工具执行按调用名取用。</summary>
    public IReadOnlyList<AIFunction> Functions => _functions;

    /// <summary>本次对话的模型请求用量与最近一次请求的上下文，请求发出即计入次数，返回用量后更新累计与上下文。</summary>
    public ExecutionUsage Usage => _usage;

    /// <summary>本次执行是否从恢复数据开始。</summary>
    public bool IsRestored => _isRestored;

    /// <summary>一轮请求。input 非空时作为本轮输入写入历史；工具结果回填后不再给出输入。
    /// 只报成功或失败，回复中的工具调用交给调用方执行。</summary>
    public async Task<ErrorOr<TurnReply>> AskAsync(string? input, CancellationToken cancellationToken)
    {
        if (input is not null)
        {
            _journal.AppendPrompt(input);
        }

        if (_source.Model is not { } model)
        {
            return [ModelErrors.ModelNotSelected()];
        }

        ErrorOr<ITurnClient> client = _clients.Resolve(model);
        if (client.IsError)
        {
            return client.ErrorsOrEmptyList;
        }

        _roundStart = _messages.Count;
        _roundOpen = true;
        if (input is not null)
        {
            _messages.Add(new ChatMessage(ChatRole.User, input));
        }

        ChatOptions options = new()
        {
            Tools = [.. _functions],
            Instructions = _source.SystemPrompt,
        };

        List<ChatResponseUpdate> updates = [];
        TokenUsage? usage = null;
        _usage = _usage.BeginRequest();
        try
        {
            await foreach (ChatResponseUpdate update in client.Value.StreamAsync([.. _messages], options, cancellationToken))
            {
                updates.Add(update);
                RecordContent(update, _journal);
                usage = UsageOf(update) ?? usage;
            }
        }
        catch
        {
            CommitPartial(updates);
            throw;
        }
        finally
        {
            _usage = _usage.CompleteRequest(usage);
        }

        return CompleteRound(updates);
    }

    /// <summary>把本轮工具结果写回历史，作为下一次请求的工具消息。</summary>
    public void AppendResults(IReadOnlyList<FunctionResultContent> results) =>
        _messages.Add(new ChatMessage(ChatRole.Tool, [.. results]));

    /// <summary>丢弃尚未收口的一轮，回到本轮开始前的历史。轮次已收口时不动作。</summary>
    public void DiscardRound()
    {
        if (!_roundOpen)
        {
            return;
        }

        if (_messages.Count > _roundStart)
        {
            _messages.RemoveRange(_roundStart, _messages.Count - _roundStart);
        }

        _roundOpen = false;
        _restoredIndex = _messages.Count > 0 && IsRestorableReply(_messages[^1])
            ? _messages.Count - 1
            : -1;
    }

    /// <summary>生成本次执行可恢复的数据，历史末尾不合法时为空。</summary>
    public LlmExecutionState? CreateRestoreData()
    {
        if (_source.Model is not { } model || !CanCreateRestoreData())
        {
            return null;
        }

        return new LlmExecutionState(
            model.Value,
            [.. _messages.Select(LlmExecutionStateCodec.ToState).Where(message => message.Contents.Count > 0)]);
    }

    /// <summary>历史末尾是否可以从下一次请求恢复。</summary>
    private bool CanCreateRestoreData()
    {
        if (_messages.Count == 0)
        {
            return false;
        }

        ChatMessage last = _messages[^1];
        if (last.Role == ChatRole.User || last.Role == ChatRole.Tool)
        {
            return true;
        }

        return IsRestorableReply(last);
    }

    /// <summary>一条助手消息是否只含可恢复的文本内容。</summary>
    private static bool IsRestorableReply(ChatMessage message) =>
        message.Role == ChatRole.Assistant
        && !message.Contents.OfType<FunctionCallContent>().Any()
        && message.Contents.Any(content => content is TextContent or TextReasoningContent);

    /// <summary>收下本轮：正常回复并入历史，已被中断过的助手消息与后续内容合并。</summary>
    private TurnReply CompleteRound(List<ChatResponseUpdate> updates)
    {
        ChatMessage? reply = Reply(updates);
        if (reply is null)
        {
            if (_restoredIndex < 0)
            {
                DiscardRound();
                _journal.AppendDiscarded();
            }
            else
            {
                _roundOpen = false;
            }

            return new TurnReply([]);
        }

        if (_restoredIndex >= 0
            && _restoredIndex == _messages.Count - 1
            && IsRestorableReply(_messages[^1]))
        {
            ChatMessage last = _messages[^1];
            _messages[^1] = new ChatMessage(ChatRole.Assistant, [.. last.Contents, .. reply.Contents]);
        }
        else
        {
            _messages.Add(reply);
        }

        _roundOpen = false;

        return new TurnReply([.. reply.Contents.OfType<FunctionCallContent>()]);
    }

    /// <summary>把中断前已收到的文本并入历史末尾的助手消息，没有文本时不动作。</summary>
    private void CommitPartial(List<ChatResponseUpdate> updates)
    {
        List<AIContent> contents =
        [
            .. updates
                .SelectMany(update => update.Contents)
                .Where(content => content is TextContent or TextReasoningContent),
        ];

        if (contents.Count == 0)
        {
            return;
        }

        if (_restoredIndex >= 0
            && _restoredIndex == _messages.Count - 1
            && IsRestorableReply(_messages[^1]))
        {
            ChatMessage last = _messages[^1];
            _messages[^1] = new ChatMessage(ChatRole.Assistant, [.. last.Contents, .. contents]);

            return;
        }

        _messages.Add(new ChatMessage(ChatRole.Assistant, contents));
        _restoredIndex = _messages.Count - 1;
    }

    /// <summary>从一条流式更新里取本次请求的用量，没有时为空。同一请求以最后一次为准。</summary>
    private static TokenUsage? UsageOf(ChatResponseUpdate update) =>
        update.Contents.OfType<UsageContent>().LastOrDefault() is { Details: { } details }
            ? TokenUsageOf(details)
            : null;

    /// <summary>把模型侧用量转成执行侧用量，合计缺失时由输入与输出补齐。</summary>
    private static TokenUsage TokenUsageOf(UsageDetails details)
    {
        long? input = details.InputTokenCount;
        long? output = details.OutputTokenCount;

        return new TokenUsage(
            input,
            output,
            details.TotalTokenCount ?? (input is { } inputCount && output is { } outputCount
                ? inputCount + outputCount
                : null),
            details.CachedInputTokenCount,
            details.ReasoningTokenCount);
    }

    /// <summary>把一条流式更新中的思考与回复内容按种类写进过程记录，其余内容由会话历史承载。</summary>
    internal static void RecordContent(ChatResponseUpdate update, ProcessJournal journal)
    {
        foreach (AIContent content in update.Contents)
        {
            switch (content)
            {
                case TextReasoningContent { Text.Length: > 0 } reasoning:
                    journal.AppendContent(ProcessContentKind.Reasoning, reasoning.Text);
                    break;

                case TextContent { Text.Length: > 0 } text:
                    journal.AppendContent(ProcessContentKind.Reply, text.Text);
                    break;
            }
        }
    }

    /// <summary>聚合本轮更新，没有内容时为空。</summary>
    private static ChatMessage? Reply(List<ChatResponseUpdate> updates)
    {
        if (updates.Count == 0)
        {
            return null;
        }

        IList<ChatMessage> messages = updates.ToChatResponse().Messages;
        if (messages.Count == 1)
        {
            return messages[0].Contents.Count == 0 ? null : messages[0];
        }

        List<AIContent> contents = [.. messages.SelectMany(message => message.Contents)];

        return contents.Count == 0 ? null : new ChatMessage(ChatRole.Assistant, contents);
    }
}

