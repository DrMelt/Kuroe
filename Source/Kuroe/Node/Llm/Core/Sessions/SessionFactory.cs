using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>会话装配入口。执行器经它取会话，内部实现类型不外露。</summary>
/// <remarks>按模型接入、工具面与 MAF 会话建立一次执行。</remarks>
public sealed class SessionFactory(IModelAccess access, IToolFactory tools, IToolJson json) : IDisposable
{
    private readonly ClientProvider _clients = new(access);
    private readonly IToolFactory _tools = tools;
    private readonly IToolJson _json = json;

    /// <summary>一次执行的会话，工具载体与历史都由执行输入给出。带恢复数据时恢复 MAF 会话。</summary>
    internal async Task<ErrorOr<IConversation>> ForExecutionAsync(
        ExecutionId execution,
        ExecutionInput context,
        ProcessJournal journal,
        LlmAgentSessionPayload? restored,
        CancellationToken cancellationToken)
    {
        ExecutionHistory history = new(context);
        if (history.Model is not { } model)
        {
            return [ModelErrors.ModelNotSelected()];
        }

        ErrorOr<ResolvedClient> resolved = _clients.Resolve(model);
        if (resolved.IsError)
        {
            return [.. resolved.Errors];
        }

        InMemoryChatHistoryProvider provider = new(new InMemoryChatHistoryProviderOptions());
        ExecutionChatClient executionClient = new(resolved.Value.Client, journal);
        ToolCollection collection = new(_tools.Create(execution, context), _json);
        ChatClientAgent agent = CreateAgent(
            executionClient,
            provider,
            history.SystemPrompt,
            collection.Build(journal, history.ToolPaths));

        if (restored is not null)
        {
            return await RestoreAsync(
                agent,
                provider,
                history,
                journal,
                executionClient,
                restored,
                cancellationToken).ConfigureAwait(false);
        }

        AgentSession created = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        List<ChatMessage> initial = [];
        history.LayOut(initial);
        provider.SetMessages(created, initial);

        return new Session(agent, created, provider, history, journal, executionClient, restored: false);
    }

    /// <summary>装配一次执行的模型客户端管线：工具调用在会话内完成，历史按服务调用持久化。</summary>
    internal static ChatClientAgent CreateAgent(
        ExecutionChatClient executionClient,
        InMemoryChatHistoryProvider provider,
        string? systemPrompt,
        IReadOnlyList<AIFunction> functions)
    {
        IChatClient agentClient = executionClient
            .AsBuilder()
            .UseFunctionInvocation(NullLoggerFactory.Instance, options =>
            {
                options.MaximumIterationsPerRequest = int.MaxValue;
                options.MaximumConsecutiveErrorsPerRequest = 1;
                options.AllowConcurrentInvocation = false;
                options.TerminateOnUnknownCalls = false;
            })
            .UsePerServiceCallChatHistoryPersistence()
            .Build();

        return new ChatClientAgent(
            agentClient,
            new ChatClientAgentOptions
            {
                Name = "KuroeLlm",
                ChatOptions = new ChatOptions
                {
                    Tools = [.. functions],
                    Instructions = systemPrompt,
                },
                ChatHistoryProvider = provider,
                UseProvidedChatClientAsIs = true,
                RequirePerServiceCallChatHistoryPersistence = true,
            },
            NullLoggerFactory.Instance,
            null);
    }

    /// <summary>恢复一次执行的会话。历史可继续性在建立恢复会话时校验。</summary>
    private static async Task<ErrorOr<IConversation>> RestoreAsync(
        ChatClientAgent agent,
        InMemoryChatHistoryProvider provider,
        ISessionHistory history,
        ProcessJournal journal,
        ExecutionChatClient executionClient,
        LlmAgentSessionPayload restored,
        CancellationToken cancellationToken)
    {
        AgentSession session;
        try
        {
            session = await AgentSessionCodec
                .RestoreAsync(agent, restored, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error.Failure("Llm.Restore", $"会话恢复失败：{ex.Message}");
        }

        if (!AgentSessionCodec.CanContinue(session))
        {
            return Error.Failure("Llm.Restore", "恢复数据中的会话历史不能继续。");
        }

        return new Session(agent, session, provider, history, journal, executionClient, restored: true);
    }

    /// <summary>释放缓存的模型客户端。</summary>
    public void Dispose() => _clients.Dispose();
}
