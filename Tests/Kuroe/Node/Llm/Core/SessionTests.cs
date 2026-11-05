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

    private static Session Create(ITurnClient client, ProcessJournal? journal = null) =>
        new(
            new Source(client),
            new ToolCollection([new FakeTool("GetLocalTime")], new ToolJson()),
            new History(),
            journal ?? new ProcessJournal());

    private static ChatResponseUpdate[] Reply(string text) =>
        [new ChatResponseUpdate(ChatRole.Assistant, text)];

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

    private sealed class FakeTool(params string[] paths) : ITool
    {
        public IReadOnlyList<ToolFunction> Functions { get; } =
            [.. paths.Select(path => new ToolDefinition(new ToolPath(path), "测试工具", []).Bind(_ => "结果"))];
    }
}
