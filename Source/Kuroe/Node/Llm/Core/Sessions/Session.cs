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

        ChatResponse response;
        TokenUsage? usage = null;
        _usage = _usage.BeginRequest();
        try
        {
            response = await client.Value.RespondAsync([.. _messages], options, cancellationToken);
            usage = response.Usage is { } details ? TokenUsageOf(details) : null;
            RecordContent(response, _journal);
        }
        finally
        {
            _usage = _usage.CompleteRequest(usage);
        }

        return CompleteRound(response);
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
    private TurnReply CompleteRound(ChatResponse response)
    {
        ChatMessage? reply = Reply(response);
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

    /// <summary>把本轮回复中的思考与回复内容按种类写进过程记录，其余内容由会话历史承载。</summary>
    internal static void RecordContent(ChatResponse response, ProcessJournal journal)
    {
        foreach (AIContent content in response.Messages.SelectMany(message => message.Contents))
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

    /// <summary>把本轮回复写成一条消息，用量内容不进入历史，没有回复内容时为空。</summary>
    private static ChatMessage? Reply(ChatResponse response)
    {
        IList<ChatMessage> messages = response.Messages;
        if (messages.Count == 0)
        {
            return null;
        }

        if (messages.Count == 1)
        {
            List<AIContent> single = [.. messages[0].Contents.Where(content => content is not UsageContent)];

            return single.Count == 0 ? null : new ChatMessage(messages[0].Role, single);
        }

        List<AIContent> contents =
        [
            .. messages
                .SelectMany(message => message.Contents)
                .Where(content => content is not UsageContent),
        ];

        return contents.Count == 0 ? null : new ChatMessage(ChatRole.Assistant, contents);
    }
}

