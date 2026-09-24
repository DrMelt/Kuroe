using System.Text;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Agent.Tools;
using Kuroe.Agent.Turns;
using Kuroe.Catalogs;
using Kuroe.Configuration;
using Kuroe.Shared.Agent.Turns;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Kuroe.Agent.Sessions;

/// <summary>一段连续的模型对话。历史由框架会话承载，来源由 <see cref="ISessionHistory"/> 决定。
/// 会话负责创建框架会话、逐轮请求并把本轮消息留在历史里。</summary>
public sealed class AgentSession
{
    private readonly AgentProvider _clients;
    private readonly SettingsProvider _settings;
    private readonly CatalogService _catalog;
    private readonly ToolCollection _tools;
    private readonly ISessionHistory _history;
    private readonly List<ChatMessage> _adopts = [];
    private ChatClientAgentSession? _session;

    internal AgentSession(
        AgentProvider clients,
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
    }

    /// <summary>上一轮的输入与输出是否未计入上下文。</summary>
    public bool LastTurnDiscarded { get; private set; }

    /// <summary>丢弃当前上下文，回到该历史来源的起点。</summary>
    public void Reset()
    {
        _adopts.Clear();
        if (_session is not null)
        {
            Relayout();
        }
    }

    /// <summary>把外部结论作为一条用户消息接进历史。框架会话尚未创建时先记下来，创建时一并写入。</summary>
    internal void Adopt(string text)
    {
        var message = new ChatMessage(ChatRole.User, text);
        if (_session is not null && _session.TryGetInMemoryChatHistory(out List<ChatMessage>? history))
        {
            history.Add(message);
        }
        else
        {
            _adopts.Add(message);
        }
    }

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

        ChatClientAgent agent = _clients.GetAgent(model, resolved.Value);
        await EnsureSessionAsync(agent, cancellationToken);
        if (_history.Stale)
        {
            Relayout();
        }

        int historyStart = HistoryCount;
        var updates = new List<AgentResponseUpdate>();
        var reply = new StringBuilder();
        try
        {
            await foreach (var update in agent.RunStreamingAsync(
                [new ChatMessage(ChatRole.User, input)],
                _session,
                Options(scope),
                cancellationToken))
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
            CompleteTurn(updates, scope.Journal, historyStart);
        }

        return reply.ToString();
    }

    /// <summary>首次使用时创建框架会话并按来源布局历史，顺带写入来源确定后已采纳的结论。</summary>
    private async Task EnsureSessionAsync(ChatClientAgent agent, CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return;
        }

        _session = (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
        Relayout();
        if (_adopts.Count > 0 && _session.TryGetInMemoryChatHistory(out List<ChatMessage>? history))
        {
            history.AddRange(_adopts);
            _adopts.Clear();
        }
    }

    /// <summary>按历史来源重铺会话历史。</summary>
    private void Relayout()
    {
        List<ChatMessage> layout = [];
        _history.LayOut(layout);
        _session!.SetInMemoryChatHistory(layout);
    }

    /// <summary>会话历史当前的条数，收口回退的本轮增量以此为界。</summary>
    private int HistoryCount =>
        _session!.TryGetInMemoryChatHistory(out List<ChatMessage>? history) ? history.Count : 0;

    /// <summary>本轮消息由框架写回历史。工具调用缺少结果或没有任何产出时整轮退回，本轮新增全部撤销。</summary>
    private void CompleteTurn(List<AgentResponseUpdate> updates, TurnJournal journal, int historyStart)
    {
        if (updates.Count == 0)
        {
            DiscardTurn(journal, historyStart);
            return;
        }

        IList<ChatMessage> produced = updates.ToAgentResponse().Messages;
        if (HasPendingFunctionCall(produced))
        {
            DiscardTurn(journal, historyStart);
            return;
        }
    }

    /// <summary>撤掉本轮期间写入历史的消息，该轮输出保留在记录里。</summary>
    private void DiscardTurn(TurnJournal journal, int historyStart)
    {
        LastTurnDiscarded = true;
        if (_session!.TryGetInMemoryChatHistory(out List<ChatMessage>? history) && history.Count > historyStart)
        {
            history.RemoveRange(historyStart, history.Count - historyStart);
        }

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
    private ChatClientAgentRunOptions Options(TurnScope scope)
    {
        AgentSettings current = _settings.Current.Agent;

        return new ChatClientAgentRunOptions(new ChatOptions
        {
            Tools = [.. _tools.Build(scope, scope.Sink)],
            Temperature = current.Temperature,
            MaxOutputTokens = current.MaxOutputTokens,
        });
    }
}

