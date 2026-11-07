using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>回合循环：模型请求工具时执行并回填，直到模型给出回复；请求失败停止循环。</summary>
public sealed class TurnLoopTests
{
    [Fact]
    public async Task Tool_calls_run_until_the_model_replies()
    {
        Conversation conversation = new(Calls(("call-1", "GetLocalTime")), new TurnReply([]));
        RecordingInvoker invoker = new();

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, invoker, "开始", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal(["开始", null], conversation.Asked);
        Assert.Equal("call-1", Assert.Single(Assert.Single(conversation.Appended)).CallId);
        Assert.Single(invoker.Invoked);
    }

    [Fact]
    public async Task The_loop_runs_until_the_model_replies()
    {
        Conversation conversation = new(
            Calls(("call-1", "GetLocalTime")),
            Calls(("call-2", "GetLocalTime")),
            new TurnReply([]));

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, new RecordingInvoker(), "开始", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal(3, conversation.Asked.Count);
        Assert.Equal(2, conversation.Appended.Count);
    }

    [Fact]
    public async Task A_failed_request_stops_the_loop()
    {
        Conversation conversation = new(Error.Failure("Model.Failed", "模型失败"));
        RecordingInvoker invoker = new();

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, invoker, "开始", CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("Model.Failed", outcome.FirstError.Code);
        Assert.Single(conversation.Asked);
        Assert.Empty(invoker.Invoked);
    }

    private static TurnReply Calls(params (string CallId, string Name)[] calls) =>
        new([.. calls.Select(call => new FunctionCallContent(call.CallId, call.Name))]);

    private sealed class Conversation(params ErrorOr<TurnReply>[] replies) : IConversation
    {
        private readonly Queue<ErrorOr<TurnReply>> _replies = new(replies);

        public List<string?> Asked { get; } = [];

        public List<IReadOnlyList<FunctionResultContent>> Appended { get; } = [];

        public ExecutionUsage Usage { get; private set; } = ExecutionUsage.None;

        public Task<ErrorOr<TurnReply>> AskAsync(string? input, CancellationToken cancellationToken)
        {
            Asked.Add(input);
            Usage = Usage.BeginRequest();

            return Task.FromResult(_replies.Dequeue());
        }

        public void AppendResults(IReadOnlyList<FunctionResultContent> results) => Appended.Add(results);
    }

    private sealed class RecordingInvoker : IToolInvoker
    {
        public List<IReadOnlyList<FunctionCallContent>> Invoked { get; } = [];

        public Task<IReadOnlyList<FunctionResultContent>> InvokeAsync(
            IReadOnlyList<FunctionCallContent> calls,
            CancellationToken cancellationToken)
        {
            Invoked.Add(calls);

            return Task.FromResult<IReadOnlyList<FunctionResultContent>>(
                [.. calls.Select(call => new FunctionResultContent(call.CallId, "完成"))]);
        }
    }
}