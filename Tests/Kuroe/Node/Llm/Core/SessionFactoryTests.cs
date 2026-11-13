using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>会话装配：模型缺失与接入错误在建立会话前给出，工具调用包住历史持久化。</summary>
public sealed class SessionFactoryTests
{
    [Fact]
    public async Task For_execution_reports_a_missing_model()
    {
        using SessionFactory factory = new(new Access(), new ToolFactory(), new ToolJson());

        ErrorOr<IConversation> created = await factory.ForExecutionAsync(
            new ExecutionId(1),
            Input(declaration: null),
            new ProcessJournal(),
            null,
            CancellationToken.None);

        Assert.True(created.IsError);
        Assert.Equal("Runtime.Model", created.FirstError.Code);
    }

    [Fact]
    public async Task For_execution_reports_the_access_error()
    {
        using SessionFactory factory = new(new Access(), new ToolFactory(), new ToolJson());

        ErrorOr<IConversation> created = await factory.ForExecutionAsync(
            new ExecutionId(1),
            Input(new KuroeNodeDeclaration(ModelName.Create("fake").Value)),
            new ProcessJournal(),
            null,
            CancellationToken.None);

        Assert.True(created.IsError);
        Assert.Equal("Model.Missing", created.FirstError.Code);
    }

    [Fact]
    public void Function_invocation_wraps_the_history_persisting_client()
    {
        InMemoryChatHistoryProvider provider = new(new InMemoryChatHistoryProviderOptions());
        ChatClientAgent agent = SessionFactory.CreateAgent(
            new ExecutionChatClient(new StubClient(), new ProcessJournal()),
            provider,
            systemPrompt: null,
            functions: []);

        Assert.IsType<FunctionInvokingChatClient>(agent.ChatClient);
        Assert.NotNull(agent.ChatClient.GetService<PerServiceCallChatHistoryPersistingChatClient>());
    }

    private static ExecutionInput Input(INodeDeclaration? declaration) => new()
    {
        Task = new TaskId(1),
        Node = NodePath.Of("实施"),
        Type = new LlmNodeType(parameters: null),
        Declaration = declaration,
    };

    private sealed class Access : IModelAccess
    {
        public ErrorOr<ModelConnection> Connect(ModelName model) =>
            Error.Validation("Model.Missing", "模型不存在。");

        public ErrorOr<Success> Check(ModelName model) => Result.Success;
    }

    private sealed class ToolFactory : IToolFactory
    {
        public IReadOnlyList<ITool> Create(ExecutionId execution, ExecutionInput input) => [];
    }

    private sealed class ToolJson : IToolJson
    {
        public JsonElement Schema(IReadOnlyList<ToolParameter> parameters) =>
            JsonSerializer.SerializeToElement(new { });

        public string Describe(AIFunctionArguments arguments) => string.Empty;
    }

    private sealed class StubClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse());

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}