using System.Text;
using ApiHub.Models;
using ErrorOr;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent;

/// <summary>一段连续对话：维护消息历史，向模型发起流式请求。</summary>
public sealed class AgentSession(
    AgentClientProvider clients,
    SettingsProvider settings,
    CatalogService catalog,
    ToolCollection tools)
{
    private readonly AgentClientProvider _clients = clients;
    private readonly SettingsProvider _settings = settings;
    private readonly CatalogService _catalog = catalog;
    private readonly ToolCollection _tools = tools;
    private readonly List<ChatMessage> _messages = [];
    private AgentSettings? _history;

    /// <summary>上一轮的输入与输出是否未计入上下文。</summary>
    public bool LastTurnDiscarded { get; private set; }

    /// <summary>丢弃上下文，系统提示词取自当前设置。</summary>
    public void Reset() => RebuildHistory(_settings.Current.Agent);

    /// <summary>一轮问答。onText 接收增量文本，返回值是完整回复或错误。</summary>
    public async Task<ErrorOr<string>> AskAsync(
        string input,
        Action<string>? onText = null,
        CancellationToken cancellationToken = default)
    {
        LastTurnDiscarded = false;

        AgentSettings current = _settings.Current.Agent;
        if (current.Model is not { } model)
        {
            return [AgentErrors.ModelNotSelected()];
        }

        ErrorOr<ModelConnection> resolved = _catalog.Connect(model);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (_history is null || current.InvalidatesHistory(_history))
        {
            RebuildHistory(current);
        }

        IChatClient client = _clients.GetClient(resolved.Value);
        _messages.Add(new ChatMessage(ChatRole.User, input));

        var updates = new List<ChatResponseUpdate>();
        var reply = new StringBuilder();
        try
        {
            await foreach (var update in client.GetStreamingResponseAsync(_messages, Options(), cancellationToken))
            {
                updates.Add(update);
                if (update.Text is { Length: > 0 } text)
                {
                    reply.Append(text);
                    onText?.Invoke(text);
                }
            }
        }
        finally
        {
            CompleteTurn(updates);
        }

        return reply.ToString();
    }

    /// <summary>本轮消息写回历史。工具调用缺少结果时整轮退回，本轮的用户输入随之撤销。</summary>
    private void CompleteTurn(List<ChatResponseUpdate> updates)
    {
        if (updates.Count == 0)
        {
            DiscardTurn();
            return;
        }

        IList<ChatMessage> produced = updates.ToChatResponse().Messages;
        if (HasPendingFunctionCall(produced))
        {
            DiscardTurn();
            return;
        }

        _messages.AddRange(produced);
    }

    /// <summary>撤销本轮加入的用户消息，该轮输出保留在界面。</summary>
    private void DiscardTurn()
    {
        LastTurnDiscarded = true;
        _messages.RemoveAt(_messages.Count - 1);
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

    /// <summary>按给定的设置重建历史。</summary>
    private void RebuildHistory(AgentSettings current)
    {
        _history = current;

        _messages.Clear();
        _messages.Add(new ChatMessage(ChatRole.System, current.SystemPrompt));
    }

    /// <summary>请求选项逐轮取自当前设置，工具集合固定。</summary>
    private ChatOptions Options()
    {
        AgentSettings current = _settings.Current.Agent;

        return new ChatOptions
        {
            Tools = [.. _tools.Tools],
            Temperature = current.Temperature,
            MaxOutputTokens = current.MaxOutputTokens,
        };
    }
}

