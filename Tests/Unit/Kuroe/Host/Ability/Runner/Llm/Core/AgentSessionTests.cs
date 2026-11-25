using System.Text.Json;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Host.Ability.Tool;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;
using Kuroe.Kernel.Execution.Shared.Conversation;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Usage;

namespace Kuroe.Host.Ability.Runner.Llm.Tests;

/// <summary>MAF 会话的捕获与恢复、用量与过程记录、未知工具继续与历史规范化。</summary>
public sealed class AgentSessionTests
{
    [Fact(DisplayName = "会话捕获与恢复往返历史")]
    public async Task Session_capture_and_restore_round_trip_the_history()
    {
        FakeClient client = new(_ => Reply("回答"));
        TestSession created = await CreateSession(client);

        LlmAgentSessionPayload captured = await AgentSessionCodec.CaptureAsync(
            created.Agent,
            created.AgentSession,
            "fake",
            CancellationToken.None);
        LlmAgentSessionPayload decoded = AgentSessionCodec.Decode(AgentSessionCodec.Encode(captured));
        AgentSession restored = await AgentSessionCodec.RestoreAsync(created.Agent, decoded, CancellationToken.None);

        List<ChatMessage> messages = created.Provider.GetMessages(restored);
        Assert.Equal("fake", decoded.Model);
        Assert.Equal("开始", Assert.Single(messages).Text);
        Assert.True(AgentSessionCodec.CanContinue(restored));
    }

    [Fact(DisplayName = "带种子的会话把轮次输入追加到恢复历史")]
    public async Task A_seeded_session_appends_the_round_input_to_the_restored_history()
    {
        FakeClient client = new(_ => Reply("回答"));
        TestSession created = await CreateSession(client);
        LlmAgentSessionPayload captured = await AgentSessionCodec.CaptureAsync(
            created.Agent,
            created.AgentSession,
            "fake",
            CancellationToken.None);

        (ChatClientAgent agent, InMemoryChatHistoryProvider provider, ExecutionChatClient executionClient) =
            CreateAgent(client, new ProcessJournal(TimeProvider.System), tools: null);
        AgentSession restored = await AgentSessionCodec.RestoreAsync(
            agent,
            AgentSessionCodec.Decode(AgentSessionCodec.Encode(captured)),
            CancellationToken.None);

        ExecutionRequest request = new(
            new ExecutionOwnerId("1"),
            new ExecutionStepId("回话"),
            [],
            [new ExecutionInputValue(new ExecutionPortName("回答"), "追问", null)],
            new Dictionary<ExecutionPortName, string>(),
            []);
        ExecutionHistory history = new(
            request,
            new LlmExecutionParameters(null, [], [], new Dictionary<string, IReadOnlyDictionary<string, string>>(), "fake"));
        List<ChatMessage> messages = provider.GetMessages(restored);
        history.LayOut(messages);
        provider.SetMessages(restored, messages);

        Session session = new(
            agent,
            restored,
            provider,
            history,
            new ProcessJournal(TimeProvider.System),
            executionClient,
            restored: false);

        await session.AskAsync(null, CancellationToken.None);

        Assert.Equal(["开始", "追问"], client.Requests[^1].Select(message => message.Text));
    }

    [Fact(DisplayName = "已消费请求记录用量与内容")]
    public async Task A_consumed_request_records_usage_and_content()
    {
        ProcessJournal journal = new(TimeProvider.System);
        FakeClient client = new(_ => UsageReply("先想", "再答"));
        TestSession created = await CreateSession(client, journal);

        await created.Session.AskAsync("问题", CancellationToken.None);

        Assert.Equal(1, created.Client.Usage.RequestCount);
        Assert.Equal(15, created.Client.Usage.Tokens.Total);
        Assert.Equal(10, created.Client.Usage.ContextTokens);
        Assert.Equal(
            ["输入", "思考", "回复"],
            journal.Entries.Select(entry => entry.Label));
        Assert.Equal(["问题", "先想", "再答"], journal.Entries.Select(entry => entry.Text));
    }

    [Fact(DisplayName = "空回复丢弃该轮")]
    public async Task An_empty_reply_discards_the_round()
    {
        ProcessJournal journal = new(TimeProvider.System);
        FakeClient client = new(_ => new ChatResponse());
        TestSession created = await CreateSession(client, journal);

        await created.Session.AskAsync("问题", CancellationToken.None);

        List<ChatMessage> messages = created.Provider.GetMessages(created.AgentSession);
        Assert.Equal("开始", Assert.Single(messages).Text);
        Assert.Contains(journal.Entries, entry => entry.Label == "丢弃");
    }

    [Fact(DisplayName = "未知工具不停止该轮")]
    public async Task An_unknown_tool_does_not_stop_the_round()
    {
        FakeClient client = new(
            _ => Reply(new FunctionCallContent("call-1", "missing")),
            _ => Reply("完成"));
        TestSession created = await CreateSession(client);

        await created.Session.AskAsync("问题", CancellationToken.None);

        List<ChatMessage> messages = created.Provider.GetMessages(created.AgentSession);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains(
            messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            result => result.CallId == "call-1");
        Assert.True(AgentSessionCodec.CanContinue(messages));
    }

    [Fact(DisplayName = "失败工具返回错误结果并继续")]
    public async Task A_failing_tool_returns_an_error_result_and_continues()
    {
        ProcessJournal journal = new(TimeProvider.System);
        ToolCollection collection = new([new FakeTool()], new ToolJson());
        IReadOnlyList<AIFunction> functions = collection.Build(journal, [new ToolPath("GetLocalTime")]);
        FakeClient client = new(
            _ => Reply(new FunctionCallContent("call-1", "GetLocalTime")),
            _ => Reply("完成"));
        TestSession created = await CreateSession(client, journal, functions);

        await created.Session.AskAsync("问题", CancellationToken.None);

        List<ChatMessage> messages = created.Provider.GetMessages(created.AgentSession);
        Assert.Equal(2, client.Requests.Count);
        Assert.Contains(
            messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>(),
            result => result.Result is string { Length: > 0 });
    }

    [Fact(DisplayName = "待处理的工具调用不能继续")]
    public void A_pending_tool_call_cannot_continue()
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(
                ChatRole.Assistant,
                [new FunctionCallContent("call-1", "missing")]),
        ];

        Assert.False(AgentSessionCodec.CanContinue(messages));
    }

    [Fact(DisplayName = "未知内容不阻止继续")]
    public void Unknown_content_does_not_block_continuation()
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(
                ChatRole.User,
                [new DataContent(new byte[] { 1 }, "image/png"), new TextContent("问题")]),
        ];

        Assert.True(AgentSessionCodec.CanContinue(messages));
    }

    [Fact(DisplayName = "未知负载格式被拒绝")]
    public void An_unknown_payload_format_is_rejected()
    {
        ExecutionRestoreState payload = new("llm.session.v2", "{}");

        Assert.Throws<FormatException>(() => AgentSessionCodec.Decode(payload));
    }

    [Fact(DisplayName = "失败请求把输入保留在恢复数据中")]
    public async Task A_failed_request_keeps_the_input_in_the_restore_data()
    {
        FakeClient client = new(_ => throw new InvalidOperationException("请求失败"));
        TestSession created = await CreateSession(client);

        ErrorOr<Success> outcome = await created.Session.AskAsync("问题", CancellationToken.None);

        Assert.True(outcome.IsError);
        LlmAgentSessionPayload payload = await created.Session.CreateRestoreDataAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("没有恢复数据。");
        AgentSession restored = await AgentSessionCodec.RestoreAsync(created.Agent, payload, CancellationToken.None);
        List<ChatMessage> messages = created.Provider.GetMessages(restored);
        Assert.Equal("问题", messages[^1].Text);
    }

    [Fact(DisplayName = "空指令以空消息发送")]
    public async Task An_empty_instruction_is_sent_as_an_empty_message()
    {
        FakeClient client = new(_ => Reply("回答"));
        TestSession created = await CreateSession(client);

        ErrorOr<Success> outcome = await created.Session.AskAsync(string.Empty, CancellationToken.None);

        Assert.False(outcome.IsError);
        List<ChatMessage> messages = created.Provider.GetMessages(created.AgentSession);
        Assert.Equal(3, messages.Count);
        Assert.Equal(string.Empty, messages[1].Text);
    }

    [Fact(DisplayName = "用量累加请求上下文跟随最近报告")]
    public async Task Usage_sums_requests_and_context_follows_the_latest_report()
    {
        FakeClient client = new(
            _ => Reply("答一"),
            _ => UsageReply("先想", "答二"));
        TestSession created = await CreateSession(client);

        await created.Session.AskAsync("问一", CancellationToken.None);
        await created.Session.AskAsync("问二", CancellationToken.None);

        ExecutionUsage usage = created.Client.Usage;
        Assert.Equal(2, usage.RequestCount);
        Assert.Equal(1, usage.RequestsWithoutUsage);
        Assert.Equal(10, usage.Tokens.Input);
        Assert.Equal(5, usage.Tokens.Output);
        Assert.Equal(15, usage.Tokens.Total);
        Assert.Equal(10, usage.ContextTokens);
    }

    [Fact(DisplayName = "恢复的会话合并部分回复")]
    public async Task A_restored_session_merges_the_partial_reply()
    {
        FakeClient client = new(_ => Reply("半句"), _ => Reply("续写"));
        TestSession created = await CreateSession(client);
        await created.Session.AskAsync("问题", CancellationToken.None);

        LlmAgentSessionPayload payload = await created.Session.CreateRestoreDataAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("没有恢复数据。");
        ProcessJournal journal = new(TimeProvider.System);
        (ChatClientAgent agent, InMemoryChatHistoryProvider provider, ExecutionChatClient executionClient) =
            CreateAgent(client, journal, tools: null);
        AgentSession restored = await AgentSessionCodec.RestoreAsync(agent, payload, CancellationToken.None);
        Session session = new(agent, restored, provider, new History(), journal, executionClient, restored: true);

        await session.AskAsync(null, CancellationToken.None);

        List<ChatMessage> messages = provider.GetMessages(restored);
        Assert.Equal(3, messages.Count);
        Assert.Equal("半句续写", messages[^1].Text);
    }

    [Fact(DisplayName = "会话可由新智能体恢复")]
    public async Task Session_can_be_restored_by_a_new_agent()
    {
        FakeClient client = new(_ => Reply("回答"));
        TestSession created = await CreateSession(client);
        LlmAgentSessionPayload captured = await AgentSessionCodec.CaptureAsync(
            created.Agent,
            created.AgentSession,
            "fake",
            CancellationToken.None);

        (ChatClientAgent agent, InMemoryChatHistoryProvider provider, _) =
            CreateAgent(client, new ProcessJournal(TimeProvider.System), tools: null);
        AgentSession restored = await AgentSessionCodec.RestoreAsync(
            agent,
            AgentSessionCodec.Decode(AgentSessionCodec.Encode(captured)),
            CancellationToken.None);

        Assert.Equal("开始", Assert.Single(provider.GetMessages(restored)).Text);
    }

    [Fact(DisplayName = "对话读取返回已持久化的用户与助手文本")]
    public async Task Conversation_reader_returns_persisted_user_and_assistant_text()
    {
        FakeClient client = new(_ => Reply("回答"));
        TestSession created = await CreateSession(client);
        await created.Session.AskAsync("问题", CancellationToken.None);
        LlmAgentSessionPayload payload = await created.Session.CreateRestoreDataAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("没有恢复数据。");

        ErrorOr<ExecutionConversation> read = await new ExecutionConversationReader()
            .ReadAsync(AgentSessionCodec.Encode(payload), CancellationToken.None);
        Assert.False(read.IsError);
        ExecutionConversation conversation = read.Value;

        Assert.Equal(
            [
                (ExecutionMessageRole.User, "开始"),
                (ExecutionMessageRole.User, "问题"),
                (ExecutionMessageRole.Assistant, "回答"),
            ],
            conversation.Messages.Select(message => (
                message.Role,
                string.Concat(message.Parts
                    .Where(part => part.Kind == ExecutionContentKind.Text)
                    .Select(part => part.Text)))));
    }

    [Fact(DisplayName = "对话读取保留推理与工具部分")]
    public async Task Conversation_reader_keeps_reasoning_and_tool_parts()
    {
        FakeClient client = new(_ => UsageReply("先想", "再答"));
        TestSession created = await CreateSession(client);
        await created.Session.AskAsync("问题", CancellationToken.None);
        LlmAgentSessionPayload payload = await created.Session.CreateRestoreDataAsync(CancellationToken.None)
            ?? throw new InvalidOperationException("没有恢复数据。");

        ErrorOr<ExecutionConversation> read = await new ExecutionConversationReader()
            .ReadAsync(AgentSessionCodec.Encode(payload), CancellationToken.None);
        Assert.False(read.IsError);
        ExecutionConversation conversation = read.Value;

        Assert.Contains(
            conversation.Messages.SelectMany(message => message.Parts),
            part => part.Kind == ExecutionContentKind.Reasoning && part.Text == "先想");
        Assert.Contains(
            conversation.Messages.SelectMany(message => message.Parts),
            part => part.Kind == ExecutionContentKind.Text && part.Text == "再答");
    }

    [Fact(DisplayName = "对话读取拒绝未知格式")]
    public async Task Conversation_reader_rejects_an_unknown_format()
    {
        ErrorOr<ExecutionConversation> conversation = await new ExecutionConversationReader()
            .ReadAsync(new ExecutionRestoreState("unknown", "{}"), CancellationToken.None);

        Assert.True(conversation.IsError);
    }

    private static async Task<TestSession> CreateSession(
        FakeClient client,
        ProcessJournal? journal = null,
        IReadOnlyList<AIFunction>? tools = null)
    {
        ProcessJournal target = journal ?? new ProcessJournal(TimeProvider.System);
        (ChatClientAgent agent, InMemoryChatHistoryProvider provider, ExecutionChatClient executionClient) =
            CreateAgent(client, target, tools);
        AgentSession agentSession = await agent.CreateSessionAsync();
        provider.SetMessages(agentSession, [new ChatMessage(ChatRole.User, "开始")]);

        return new TestSession(
            new Session(agent, agentSession, provider, new History(), target, executionClient, restored: false),
            agent,
            agentSession,
            provider,
            executionClient,
            client);
    }

    private static (ChatClientAgent Agent, InMemoryChatHistoryProvider Provider, ExecutionChatClient Client) CreateAgent(
        FakeClient client,
        ProcessJournal journal,
        IReadOnlyList<AIFunction>? tools)
    {
        InMemoryChatHistoryProvider provider = new(new InMemoryChatHistoryProviderOptions());
        ExecutionChatClient executionClient = new(client, journal);
        ChatClientAgent agent = SessionFactory.CreateAgent(
            executionClient,
            provider,
            systemPrompt: null,
            functions: tools ?? []);

        return (agent, provider, executionClient);
    }

    private static ChatResponse Reply(string text) =>
        new(new ChatMessage(ChatRole.Assistant, text));

    private static ChatResponse Reply(params AIContent[] contents) =>
        new(new ChatMessage(ChatRole.Assistant, contents));

    private static ChatResponse UsageReply(string reasoning, string text) =>
        new(new ChatMessage(
            ChatRole.Assistant,
            [new TextReasoningContent(reasoning), new TextContent(text)]))
        {
            Usage = new UsageDetails
            {
                InputTokenCount = 10,
                OutputTokenCount = 5,
                TotalTokenCount = 15,
            },
        };

    private sealed record TestSession(
        Session Session,
        ChatClientAgent Agent,
        AgentSession AgentSession,
        InMemoryChatHistoryProvider Provider,
        ExecutionChatClient Client,
        FakeClient RawClient);

    private sealed class History : ISessionHistory
    {
        public ModelName? Model => ModelName.Create("fake").Value;

        public string? SystemPrompt => null;

        public IReadOnlyList<ToolPath> ToolPaths => [];

        public void LayOut(List<ChatMessage> messages)
        {
        }
    }

    private sealed class FakeClient(params Func<IReadOnlyList<ChatMessage>, ChatResponse>[] replies) : IChatClient
    {
        private readonly Queue<Func<IReadOnlyList<ChatMessage>, ChatResponse>> _replies = new(replies);

        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatMessage> snapshot = [.. messages];
            Requests.Add(snapshot);

            return Task.FromResult(_replies.Count > 0
                ? _replies.Dequeue()(snapshot)
                : new ChatResponse());
        }

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

    private sealed class FakeTool : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
        [
            new ToolDefinition(new ToolPath("GetLocalTime"), "测试工具", [])
                .Bind(_ => Fail()),
        ];

        private static ErrorOr<string> Fail() =>
            throw new InvalidOperationException("坏了");
    }

    private sealed class ToolJson : IToolJson
    {
        public JsonElement Schema(IReadOnlyList<ToolParameter> parameters) =>
            JsonSerializer.SerializeToElement(new { });

        public string Describe(AIFunctionArguments arguments) => string.Empty;
    }
}
