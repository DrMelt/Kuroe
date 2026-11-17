using ErrorOr;
using Kuroe.Runner.Llm.Executions;
using Kuroe.Runner.Llm.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Kuroe.Execution.Shared.Conversation;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;

namespace Kuroe.Runner.Llm.Sessions;

/// <summary>会话装配入口。执行器经它取会话，内部实现类型不外露。</summary>
/// <remarks>按模型接入、工具面与 MAF 会话建立一次执行。</remarks>
public sealed class SessionFactory(IModelAccess access, IToolFactory tools, IToolJson json) : IDisposable
{
    private readonly ClientProvider _clients = new(access);
    private readonly IToolFactory _tools = tools;
    private readonly IToolJson _json = json;

    /// <summary>一次执行的会话，工具载体与历史都由执行输入给出。带恢复数据时恢复 MAF 会话，接续历史时恢复后追加本轮输入。</summary>
    internal async Task<ErrorOr<IConversation>> ForExecutionAsync(
        ExecutionId execution,
        ExecutionRequest request,
        LlmExecutionParameters parameters,
        ProcessJournal journal,
        LlmAgentSessionPayload? restored,
        LlmAgentSessionPayload? seeded,
        CancellationToken cancellationToken)
    {
        ExecutionHistory history = new(request, parameters);
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
        ToolCollection collection = new(_tools.Create(execution, request, parameters), _json);
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
                continuesRound: true,
                cancellationToken).ConfigureAwait(false);
        }

        if (seeded is not null)
        {
            return await RestoreAsync(
                agent,
                provider,
                history,
                journal,
                executionClient,
                seeded,
                continuesRound: false,
                cancellationToken).ConfigureAwait(false);
        }

        AgentSession created = await agent.CreateSessionAsync(cancellationToken).ConfigureAwait(false);
        List<ChatMessage> initial = [];
        history.LayOut(initial);
        provider.SetMessages(created, initial);

        return new Session(agent, created, provider, history, journal, executionClient, restored: false);
    }

    /// <summary>读取恢复数据中的会话历史。装配一个不接模型的 Agent，只用于反序列化与会话历史提供者。</summary>
    internal static async Task<ErrorOr<ExecutionConversation>> ReadConversationAsync(
        ExecutionRestoreState restore,
        CancellationToken cancellationToken)
    {
        try
        {
            LlmAgentSessionPayload payload = AgentSessionCodec.Decode(restore);
            InMemoryChatHistoryProvider provider = new(new InMemoryChatHistoryProviderOptions());
            ChatClientAgent agent = CreateAgent(
                new UnavailableChatClient(),
                provider,
                systemPrompt: null,
                functions: []);
            AgentSession session = await AgentSessionCodec
                .RestoreAsync(agent, payload, cancellationToken)
                .ConfigureAwait(false);

            return session.TryGetInMemoryChatHistory(out List<ChatMessage>? messages) && messages is not null
                ? ConversationOf(messages)
                : ExecutionConversation.Empty;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Error.Failure("Llm.Conversation", $"会话历史不可读：{ex.Message}");
        }
    }

    /// <summary>把 MAF 消息转为中性消息，保留内容种类与顺序，不做展示过滤。</summary>
    private static ExecutionConversation ConversationOf(IReadOnlyList<ChatMessage> messages)
    {
        List<ExecutionMessage> converted = [];
        foreach (ChatMessage message in messages)
        {
            List<ExecutionContentPart> parts = [];
            foreach (AIContent content in message.Contents)
            {
                switch (content)
                {
                    case TextContent text:
                        parts.Add(new ExecutionContentPart(ExecutionContentKind.Text, text.Text ?? string.Empty));
                        break;

                    case TextReasoningContent reasoning:
                        parts.Add(new ExecutionContentPart(ExecutionContentKind.Reasoning, reasoning.Text ?? string.Empty));
                        break;

                    case FunctionCallContent call:
                        parts.Add(new ExecutionContentPart(ExecutionContentKind.ToolCall, call.Name ?? string.Empty));
                        break;

                    case FunctionResultContent result:
                        parts.Add(new ExecutionContentPart(
                            ExecutionContentKind.ToolResult,
                            result.Result?.ToString() ?? string.Empty));
                        break;

                    case UsageContent:
                        break;

                    default:
                        parts.Add(new ExecutionContentPart(ExecutionContentKind.Other, string.Empty));
                        break;
                }
            }

            if (parts.Count > 0)
            {
                converted.Add(new ExecutionMessage(RoleOf(message.Role), parts));
            }
        }

        return new ExecutionConversation(converted);
    }

    private static ExecutionMessageRole RoleOf(ChatRole role)
    {
        if (role == ChatRole.User)
        {
            return ExecutionMessageRole.User;
        }

        if (role == ChatRole.Assistant)
        {
            return ExecutionMessageRole.Assistant;
        }

        if (role == ChatRole.System)
        {
            return ExecutionMessageRole.System;
        }

        return role == ChatRole.Tool ? ExecutionMessageRole.Tool : ExecutionMessageRole.Other;
    }

    /// <summary>装配一次执行的模型客户端管线：工具调用在会话内完成，历史按服务调用持久化。</summary>
    internal static ChatClientAgent CreateAgent(
        IChatClient executionClient,
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

    /// <summary>恢复一次执行的会话。历史可继续性在建立恢复会话时校验。接续历史时把本轮输入追加到恢复后的历史。</summary>
    private static async Task<ErrorOr<IConversation>> RestoreAsync(
        ChatClientAgent agent,
        InMemoryChatHistoryProvider provider,
        ExecutionHistory history,
        ProcessJournal journal,
        ExecutionChatClient executionClient,
        LlmAgentSessionPayload restored,
        bool continuesRound,
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

        if (!continuesRound)
        {
            List<ChatMessage> messages = provider.GetMessages(session);
            history.LayOut(messages);
            provider.SetMessages(session, messages);
        }

        return new Session(agent, session, provider, history, journal, executionClient, restored: continuesRound);
    }

    /// <summary>释放缓存的模型客户端。</summary>
    public void Dispose() => _clients.Dispose();
}