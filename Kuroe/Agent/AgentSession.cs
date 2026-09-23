using System.Text;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent;

/// <summary>一段连续的模型对话。历史从哪来由 <see cref="ISessionHistory"/> 决定，
/// 会话本身只管持有历史、逐轮请求并把本轮消息接回历史。</summary>
public sealed class AgentSession
{
    private readonly AgentClientProvider _clients;
    private readonly SettingsProvider _settings;
    private readonly CatalogService _catalog;
    private readonly ToolCollection _tools;
    private readonly ISessionHistory _history;
    private readonly List<ChatMessage> _messages = [];

    internal AgentSession(
        AgentClientProvider clients,
        SettingsProvider settings,
        CatalogService catalog,
        ToolCollection tools,
        ISessionHistory history)
    {
        _clients = clients;
        _settings = settings;
        _catalog = catalog;
        _tools = tools;
        _history = history;
        _history.LayOut(_messages);
    }

    /// <summary>上一轮的输入与输出是否未计入上下文。</summary>
    public bool LastTurnDiscarded { get; private set; }

    /// <summary>丢弃当前上下文，回到该历史来源的起点。</summary>
    public void Reset() => _history.LayOut(_messages);

    /// <summary>把外部结论作为一条用户消息接进历史。</summary>
    internal void Adopt(string text) => _messages.Add(new ChatMessage(ChatRole.User, text));

    /// <summary>一轮请求。scope 决定过程写到哪条记录、工具绑到哪个归属。返回值是完整回复或错误。</summary>
    public async Task<ErrorOr<string>> AskAsync(
        string input,
        TurnScope scope,
        CancellationToken cancellationToken = default)
    {
        LastTurnDiscarded = false;
        scope.Journal.Append(new PromptEntry(input));

        if (_history.Model is not { Length: > 0 } model)
        {
            return [AgentErrors.ModelNotSelected()];
        }

        ErrorOr<ModelConnection> resolved = _catalog.Connect(model);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (_history.Stale)
        {
            _history.LayOut(_messages);
        }

        IChatClient client = _clients.GetClient(model, resolved.Value);
        _messages.Add(new ChatMessage(ChatRole.User, input));

        var updates = new List<ChatResponseUpdate>();
        var reply = new StringBuilder();
        try
        {
            await foreach (var update in client.GetStreamingResponseAsync(_messages, Options(scope), cancellationToken))
            {
                updates.Add(update);
                if (update.Text is { Length: > 0 } text)
                {
                    reply.Append(text);
                    scope.Sink.OnText(text);
                }
            }
        }
        finally
        {
            CompleteTurn(updates, scope.Journal);
        }

        return reply.ToString();
    }

    /// <summary>本轮消息写回历史。工具调用缺少结果时整轮退回，本轮的用户输入随之撤销。</summary>
    private void CompleteTurn(List<ChatResponseUpdate> updates, TurnJournal journal)
    {
        if (updates.Count == 0)
        {
            DiscardTurn(journal);
            return;
        }

        IList<ChatMessage> produced = updates.ToChatResponse().Messages;
        if (HasPendingFunctionCall(produced))
        {
            DiscardTurn(journal);
            return;
        }

        _messages.AddRange(produced);
    }

    /// <summary>撤销本轮加入的用户消息，该轮输出保留在记录里。</summary>
    private void DiscardTurn(TurnJournal journal)
    {
        LastTurnDiscarded = true;
        _messages.RemoveAt(_messages.Count - 1);
        journal.Append(new DiscardedEntry());
    }

    /// <summary>消息序列中是否有缺少结果的工具调用。</summary>
    private static bool HasPendingFunctionCall(IEnumerable<ChatMessage> messages)
    {
        HashSet<string> calls = [];
        HashSet<string> results = [];

        foreach (ChatMessage message in messages)
        {
            foreach (AIContent content in message.Contents)
            {
                switch (content)
                {
                    case FunctionCallContent { CallId: { Length: > 0 } id }:
                        calls.Add(id);
                        break;

                    case FunctionResultContent { CallId: { Length: > 0 } id }:
                        results.Add(id);
                        break;
                }
            }
        }

        return !calls.IsSubsetOf(results);
    }

    /// <summary>请求选项逐轮取自当前设置，工具按本轮归属构造。</summary>
    private ChatOptions Options(TurnScope scope)
    {
        AgentSettings current = _settings.Current.Agent;

        return new ChatOptions
        {
            Tools = [.. _tools.Build(scope, scope.Sink)],
            Temperature = current.Temperature,
            MaxOutputTokens = current.MaxOutputTokens,
        };
    }
}

