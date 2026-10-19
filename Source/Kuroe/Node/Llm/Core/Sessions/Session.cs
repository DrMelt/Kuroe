using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>一段连续的模型对话。历史由框架会话承载，来源由 <see cref="ISessionHistory"/> 决定。
/// 会话负责创建框架会话、逐轮请求并把本轮消息留在历史里。</summary>
public sealed class Session
{
    private readonly ClientProvider _clients;
    private readonly IModelSettings _settings;
    private readonly IModelAccess _access;
    private readonly ToolCollection _tools;
    private readonly ISessionHistory _history;
    private ChatClientAgentSession? _session;

    internal Session(
        ClientProvider clients,
        IModelSettings settings,
        IModelAccess access,
        ToolCollection tools,
        ISessionHistory history)
    {
        _clients = clients;
        _settings = settings;
        _access = access;
        _tools = tools;
        _history = history;
    }

    /// <summary>当前是否选定了对话模型，未选定无法发起请求。</summary>
    public bool HasModel => _history.Model is not null;

    /// <summary>丢弃当前上下文，回到该历史来源的起点。</summary>
    public void Reset()
    {
        if (_session is not null)
        {
            Relayout();
        }
    }

    /// <summary>一轮请求。execution 给出工具绑定的身份与过程播发出口，journal 收本次执行的过程记录。只报成功或失败，回复文本经过程记录与过程播发送出。</summary>
    internal async Task<ErrorOr<Success>> AskAsync(
        string input,
        IExecution execution,
        ProcessJournal journal,
        CancellationToken cancellationToken = default)
    {
        journal.Append(new PromptEntry(input));

        if (_history.Model is not { } model)
        {
            return [ModelErrors.ModelNotSelected()];
        }

        ErrorOr<ModelConnection> resolved = _access.Connect(model);
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
        try
        {
            await foreach (var update in agent.RunStreamingAsync(
                [new ChatMessage(ChatRole.User, input)],
                _session,
                Options(execution, journal),
                cancellationToken))
            {
                updates.Add(update);
                if (update.Text is { Length: > 0 } text)
                {
                    journal.AppendText(text);
                    execution.Sink.OnText(text);
                }
            }
        }
        finally
        {
            CompleteReply(updates, journal, historyStart);
        }

        return Result.Success;
    }

    /// <summary>首次使用时创建框架会话并按来源布局历史。</summary>
    private async Task EnsureSessionAsync(ChatClientAgent agent, CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return;
        }

        _session = (ChatClientAgentSession)await agent.CreateSessionAsync(cancellationToken);
        Relayout();
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

    /// <summary>本轮回复的消息由框架写回历史。工具调用缺少结果或没有任何产出时整轮退回，本轮新增全部撤销。</summary>
    private void CompleteReply(List<AgentResponseUpdate> updates, ProcessJournal journal, int historyStart)
    {
        if (updates.Count == 0)
        {
            DiscardReply(journal, historyStart);
            return;
        }

        IList<ChatMessage> produced = updates.ToAgentResponse().Messages;
        if (HasPendingFunctionCall(produced))
        {
            DiscardReply(journal, historyStart);
        }
    }

    /// <summary>撤掉本轮期间写入历史的消息，该轮输出保留在记录里。</summary>
    private void DiscardReply(ProcessJournal journal, int historyStart)
    {
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

    /// <summary>请求选项逐轮取自当前设置，工具按本次执行的身份、播发出口与记录构造。</summary>
    private ChatClientAgentRunOptions Options(IExecution execution, ProcessJournal journal)
    {
        return new ChatClientAgentRunOptions(new ChatOptions
        {
            Tools = [.. _tools.Build(execution.Id, execution.Sink, journal, _history.ToolPaths)],
            Temperature = _settings.Temperature,
            MaxOutputTokens = _settings.MaxOutputTokens,
            Instructions = _history.SystemPrompt,
        });
    }
}

