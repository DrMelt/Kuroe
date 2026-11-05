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

    internal Session(
        ITurnClientSource clients,
        ToolCollection tools,
        ISessionHistory source,
        ProcessJournal journal)
    {
        _clients = clients;
        _source = source;
        _journal = journal;
        _functions = tools.Build(journal, source.ToolPaths);
        source.LayOut(_messages);
    }

    /// <summary>本轮可用的工具函数，工具执行按调用名取用。</summary>
    public IReadOnlyList<AIFunction> Functions => _functions;

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

        int start = _messages.Count;
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
        try
        {
            await foreach (ChatResponseUpdate update in client.Value.StreamAsync([.. _messages], options, cancellationToken))
            {
                updates.Add(update);
                RecordContent(update, _journal);
            }
        }
        catch
        {
            _messages.RemoveRange(start, _messages.Count - start);
            throw;
        }

        ChatMessage? reply = Reply(updates);
        if (reply is null)
        {
            _messages.RemoveRange(start, _messages.Count - start);
            _journal.AppendDiscarded();

            return new TurnReply([]);
        }

        _messages.Add(reply);

        return new TurnReply([.. reply.Contents.OfType<FunctionCallContent>()]);
    }

    /// <summary>把本轮工具结果写回历史，作为下一次请求的工具消息。</summary>
    public void AppendResults(IReadOnlyList<FunctionResultContent> results) =>
        _messages.Add(new ChatMessage(ChatRole.Tool, [.. results]));

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

