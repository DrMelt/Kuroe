using System.Runtime.CompilerServices;
using ApiHub.Shared.Models;
using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Kuroe.Node.Llm.Tools;
using Kuroe.Tool;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests;

/// <summary>会话的历史与请求：每轮发送完整历史与工具面，工具结果回填后续请求不带输入，空回复整轮退回。</summary>
public sealed class SessionTests
{
    [Fact]
    public void Reasoning_and_reply_contents_are_recorded_by_type()
    {
        ProcessJournal journal = new();
        List<ProcessContent> contents = [];
        journal.ContentAppended += contents.Add;
        ChatResponseUpdate update = new(
            ChatRole.Assistant,
            [
                new TextReasoningContent("先想"),
                new TextContent("再答"),
                new FunctionCallContent("call-1", "ReadFile"),
            ]);

        Session.RecordContent(update, journal);

        Assert.Equal(
            [
                new ProcessContent(ProcessContentKind.Reasoning, "先想"),
                new ProcessContent(ProcessContentKind.Reply, "再答"),
            ],
            contents);
        Assert.Equal(["思考", "回复"], journal.Entries.Select(entry => entry.Label));
        Assert.Equal(["先想", "再答"], journal.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public async Task History_accumulates_and_every_request_carries_the_tool_face()
    {
        RecordingTurnClient client = new(Reply("第一答"), Reply("第二答"));
        Session session = Create(client);

        await session.AskAsync("第一问", CancellationToken.None);
        await session.AskAsync("第二问", CancellationToken.None);

        Assert.Equal(["第一问"], client.Requests[0].Messages.Select(message => message.Text));
        Assert.Equal(["第一问", "第一答", "第二问"], client.Requests[1].Messages.Select(message => message.Text));
        Assert.All(client.Requests, request => Assert.Single(request.Options.Tools ?? []));
        Assert.All(client.Requests, request => Assert.Equal("系统指令", request.Options.Instructions));
    }

    [Fact]
    public async Task Tool_calls_are_returned_and_results_join_the_history()
    {
        RecordingTurnClient client = new(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "GetLocalTime")])],
            Reply("完成"));
        Session session = Create(client);

        ErrorOr<TurnReply> first = await session.AskAsync("现在几点", CancellationToken.None);
        session.AppendResults([new FunctionResultContent("call-1", "十二点")]);
        ErrorOr<TurnReply> second = await session.AskAsync(null, CancellationToken.None);

        Assert.Equal("GetLocalTime", Assert.Single(first.Value.Calls).Name);
        Assert.Empty(second.Value.Calls);

        IReadOnlyList<ChatMessage> request = client.Requests[1].Messages;
        Assert.Equal([ChatRole.User, ChatRole.Assistant, ChatRole.Tool], request.Select(message => message.Role));
        Assert.Equal("现在几点", request[0].Text);
        Assert.Equal("call-1", Assert.Single(request[1].Contents.OfType<FunctionCallContent>()).CallId);
        Assert.Equal("call-1", Assert.Single(request[2].Contents.OfType<FunctionResultContent>()).CallId);
    }

    [Fact]
    public async Task An_empty_reply_discards_the_round()
    {
        RecordingTurnClient client = new([], Reply("第二答"));
        ProcessJournal journal = new();
        Session session = Create(client, journal);

        ErrorOr<TurnReply> empty = await session.AskAsync("第一问", CancellationToken.None);
        await session.AskAsync("第二问", CancellationToken.None);

        Assert.Empty(empty.Value.Calls);
        Assert.Contains(journal.Entries, entry => entry.Label == "丢弃");
        Assert.Equal(["第二问"], client.Requests[1].Messages.Select(message => message.Text));
    }

    [Fact]
    public async Task Requests_are_counted_and_usage_is_summed()
    {
        RecordingTurnClient client = new(
            UsageReply("第一答", new UsageDetails
            {
                InputTokenCount = 100,
                OutputTokenCount = 20,
                TotalTokenCount = 120,
                CachedInputTokenCount = 80,
                ReasoningTokenCount = 5,
            }),
            Reply("第二答"));
        Session session = Create(client);

        await session.AskAsync("第一问", CancellationToken.None);
        await session.AskAsync("第二问", CancellationToken.None);

        Assert.Equal(2, session.Usage.RequestCount);
        Assert.Equal(1, session.Usage.RequestsWithoutUsage);
        Assert.Equal(100, session.Usage.Tokens.Input);
        Assert.Equal(20, session.Usage.Tokens.Output);
        Assert.Equal(120, session.Usage.Tokens.Total);
        Assert.Equal(80, session.Usage.Tokens.CachedInput);
        Assert.Equal(5, session.Usage.Tokens.Reasoning);
        Assert.Null(session.Usage.ContextTokens);
    }

    [Fact]
    public async Task Context_tokens_follow_the_latest_request()
    {
        RecordingTurnClient client = new(
            UsageReply("第一答", new UsageDetails { InputTokenCount = 100, OutputTokenCount = 20, TotalTokenCount = 120 }),
            UsageReply("第二答", new UsageDetails { InputTokenCount = 50, OutputTokenCount = 10, TotalTokenCount = 60 }));
        Session session = Create(client);

        await session.AskAsync("第一问", CancellationToken.None);
        await session.AskAsync("第二问", CancellationToken.None);

        Assert.Equal(150, session.Usage.Tokens.Input);
        Assert.Equal(50, session.Usage.ContextTokens);
    }

    [Fact]
    public async Task The_last_usage_in_a_request_wins()
    {
        RecordingTurnClient client = new(
            [
                .. Reply("答复"),
                .. UsageReply(string.Empty, new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2, TotalTokenCount = 12 }),
                .. UsageReply(string.Empty, new UsageDetails { InputTokenCount = 30, OutputTokenCount = 4, TotalTokenCount = 34 }),
            ]);
        Session session = Create(client);

        await session.AskAsync("问题", CancellationToken.None);

        Assert.Equal(1, session.Usage.RequestCount);
        Assert.Equal(0, session.Usage.RequestsWithoutUsage);
        Assert.Equal(30, session.Usage.Tokens.Input);
        Assert.Equal(4, session.Usage.Tokens.Output);
        Assert.Equal(34, session.Usage.Tokens.Total);
        Assert.Equal(30, session.Usage.ContextTokens);
    }

    [Fact]
    public async Task Usage_is_not_carried_into_the_history()
    {
        RecordingTurnClient client = new(
            UsageReply("第一答", new UsageDetails { InputTokenCount = 10, OutputTokenCount = 2, TotalTokenCount = 12 }),
            Reply("第二答"));
        Session session = Create(client);

        await session.AskAsync("第一问", CancellationToken.None);
        await session.AskAsync("第二问", CancellationToken.None);

        Assert.DoesNotContain(
            client.Requests[1].Messages.SelectMany(message => message.Contents),
            content => content is UsageContent);
    }

    [Fact]
    public async Task A_failed_stream_still_counts_the_request()
    {
        Session session = Create(new FailingTurnClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AskAsync("问题", CancellationToken.None));

        Assert.Equal(1, session.Usage.RequestCount);
        Assert.Equal(1, session.Usage.RequestsWithoutUsage);
        Assert.Null(session.Usage.ContextTokens);
    }

    [Fact]
    public async Task An_interrupted_stream_keeps_the_partial_reply_in_the_restore_data()
    {
        Session session = Create(new FailingTurnClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AskAsync("问题", CancellationToken.None));

        LlmExecutionState? state = session.CreateRestoreData();
        Assert.NotNull(state);
        Assert.Equal(ChatRole.Assistant.Value, state.Messages[^1].Role);
        Assert.Equal("半句", Assert.Single(state.Messages[^1].Contents).Text);
    }

    [Fact]
    public async Task A_restored_session_continues_from_the_partial_reply_and_merges_it()
    {
        Session original = Create(new FailingTurnClient());
        await Assert.ThrowsAsync<InvalidOperationException>(() => original.AskAsync("问题", CancellationToken.None));
        LlmExecutionState state = original.CreateRestoreData()!;

        RecordingTurnClient client = new(Reply("续写"));
        Session restored = CreateRestored(state, client);

        await restored.AskAsync(null, CancellationToken.None);

        Assert.Equal("半句", client.Requests[0].Messages[^1].Text);

        await restored.AskAsync("下一问", CancellationToken.None);

        Assert.Equal("半句续写", client.Requests[1].Messages[^2].Text);
    }

    [Fact]
    public async Task A_restore_data_round_trips_tool_calls_and_results()
    {
        RecordingTurnClient client = new(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "GetLocalTime")])]);
        Session session = Create(client);

        await session.AskAsync("现在几点", CancellationToken.None);
        session.AppendResults([new FunctionResultContent("call-1", "十二点")]);

        LlmExecutionState state = session.CreateRestoreData()!;
        IReadOnlyList<LlmContentState> contents = [.. state.Messages.SelectMany(message => message.Contents)];

        Assert.Contains(contents, content => content.Kind == "call" && content.Name == "GetLocalTime");
        Assert.Contains(contents, content => content.Kind == "result" && content.Result == "十二点");
    }

    [Fact]
    public async Task A_pending_tool_call_cannot_continue()
    {
        RecordingTurnClient client = new(
            [new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent("call-1", "GetLocalTime")])]);
        Session session = Create(client);

        await session.AskAsync("现在几点", CancellationToken.None);

        Assert.Null(session.CreateRestoreData());
    }

    [Fact]
    public async Task A_non_resumable_failure_discards_the_round()
    {
        Session session = Create(new ThrowingTurnClient());

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.AskAsync("问题", CancellationToken.None));

        session.DiscardRound();

        Assert.Null(session.CreateRestoreData());
    }

    [Fact]
    public void A_restore_data_round_trips_through_the_record_codec()
    {
        LlmExecutionState state = new(
            "fake",
            [
                new LlmMessageState("user", [new LlmContentState("text", Text: "问题")]),
                new LlmMessageState("assistant", [new LlmContentState("text", Text: "回答")]),
            ]);

        ExecutionRestorePayload encoded = LlmExecutionStateCodec.Encode(state);
        LlmExecutionState decoded = LlmExecutionStateCodec.Decode(encoded);

        Assert.Equal(LlmExecutionStateCodec.Format, encoded.Format);
        Assert.Equal("fake", decoded.Model);
        Assert.Equal("回答", decoded.Messages[^1].Contents[0].Text);
        Assert.True(LlmExecutionStateCodec.IsRestorable(decoded));
    }

    [Fact]
    public async Task A_completed_reply_produces_restore_data()
    {
        Session session = Create(new RecordingTurnClient(Reply("完成")));

        await session.AskAsync("问题", CancellationToken.None);

        LlmExecutionState? state = session.CreateRestoreData();
        Assert.NotNull(state);
        Assert.Equal("完成", state.Messages[^1].Contents[0].Text);
    }

    private static Session Create(ITurnClient client, ProcessJournal? journal = null) =>
        new(
            new Source(client),
            new ToolCollection([new FakeTool("GetLocalTime")], new ToolJson()),
            new History(),
            journal ?? new ProcessJournal());

    private static Session CreateRestored(LlmExecutionState state, ITurnClient client) =>
        new(
            new Source(client),
            new ToolCollection([new FakeTool("GetLocalTime")], new ToolJson()),
            new History(),
            new ProcessJournal(),
            [.. state.Messages.Select(LlmExecutionStateCodec.ToMessage)]);

    private static ChatResponseUpdate[] Reply(string text) =>
        [new ChatResponseUpdate(ChatRole.Assistant, text)];

    private static ChatResponseUpdate[] UsageReply(string text, UsageDetails details)
    {
        List<AIContent> contents = [new TextContent(text), new UsageContent(details)];

        return [new ChatResponseUpdate(ChatRole.Assistant, contents)];
    }

    private sealed class Source(ITurnClient client) : ITurnClientSource
    {
        public ErrorOr<ITurnClient> Resolve(ModelName model) => new DelegatingTurnClient(client);
    }

    private sealed class DelegatingTurnClient(ITurnClient client) : ITurnClient
    {
        public IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            CancellationToken cancellationToken) =>
            client.StreamAsync(messages, options, cancellationToken);
    }

    private sealed class History : ISessionHistory
    {
        public ModelName? Model => ModelName.Create("fake").Value;

        public string? SystemPrompt => "系统指令";

        public IReadOnlyList<ToolPath> ToolPaths => [new ToolPath("GetLocalTime")];

        public void LayOut(List<ChatMessage> messages)
        {
        }
    }

    private sealed record Request(IReadOnlyList<ChatMessage> Messages, ChatOptions Options);

    private sealed class RecordingTurnClient(params ChatResponseUpdate[][] replies) : ITurnClient
    {
        private readonly Queue<ChatResponseUpdate[]> _replies = new(replies);

        public List<Request> Requests { get; } = [];

        public async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(new Request([.. messages], options));

            foreach (ChatResponseUpdate update in _replies.Count > 0 ? _replies.Dequeue() : [])
            {
                await Task.Yield();
                yield return update;
            }
        }
    }

    private sealed class FailingTurnClient : ITurnClient
    {
        public async IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "半句");
            throw new InvalidOperationException("流中断。");
        }
    }

    private sealed class ThrowingTurnClient : ITurnClient
    {
        public IAsyncEnumerable<ChatResponseUpdate> StreamAsync(
            IReadOnlyList<ChatMessage> messages,
            ChatOptions options,
            CancellationToken cancellationToken) =>
            new ThrowingEnumerable();

        private sealed class ThrowingEnumerable : IAsyncEnumerable<ChatResponseUpdate>
        {
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
                new ThrowingEnumerator();
        }

        private sealed class ThrowingEnumerator : IAsyncEnumerator<ChatResponseUpdate>
        {
            public ChatResponseUpdate Current => throw new InvalidOperationException("请求失败。");

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public ValueTask<bool> MoveNextAsync() => throw new InvalidOperationException("请求失败。");
        }
    }

    private sealed class FakeTool(params string[] paths) : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
            [.. paths.Select(path => new ToolDefinition(new ToolPath(path), "测试工具", []).Bind(_ => "结果"))];
    }
}
