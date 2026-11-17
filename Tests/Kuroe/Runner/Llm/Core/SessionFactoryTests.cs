using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Tool;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Ports;
using Kuroe.Execution.Shared.Request;

namespace Kuroe.Runner.Llm.Tests;

/// <summary>会话装配：模型缺失与接入错误在建立会话前给出，工具调用包住历史持久化。</summary>
public sealed class SessionFactoryTests
{
    [Fact]
    public async Task For_execution_reports_a_missing_model()
    {
        using SessionFactory factory = new(new Access(), new ToolFactory(), new ToolJson());

        ErrorOr<IConversation> created = await factory.ForExecutionAsync(
            new ExecutionId(1),
            Request(),
            Parameters(null),
            new ProcessJournal(),
            null,
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
            Request(),
            Parameters("fake"),
            new ProcessJournal(),
            null,
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

    private static ExecutionRequest Request() => new(
        new ExecutionOwnerId("1"),
        new ExecutionTargetId("实施"),
        [],
        [],
        new Dictionary<ExecutionPortName, string>(),
        []);

    private static LlmExecutionParameters Parameters(string? model) =>
        new(null, [], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), model);

    private sealed class Access : IModelAccess
    {
        public ErrorOr<ModelConnection> Connect(ModelName model) =>
            Error.Validation("Model.Missing", "模型不存在。");

        public ErrorOr<Success> Check(ModelName model) => Result.Success;
    }

    private sealed class ToolFactory : IToolFactory
    {
        public IReadOnlyList<ITool> Create(
            ExecutionId execution,
            ExecutionRequest request,
            LlmExecutionParameters parameters) => [];
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
