using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Node.Llm.Executions;
using Kuroe.Node.Llm.Sessions;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Node.Llm.Tests.Executions;

/// <summary>回合循环：模型请求工具时执行并回填，直到模型给出回复；次数用尽与请求失败都停止循环。</summary>
public sealed class TurnLoopTests
{
    [Fact]
    public async Task Tool_calls_run_until_the_model_replies()
    {
        Conversation conversation = new(Calls(("call-1", "GetLocalTime")), new TurnReply([]));
        RecordingInvoker invoker = new();

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, invoker, new TurnBudget(5), "开始", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal(["开始", null], conversation.Asked);
        Assert.Equal("call-1", Assert.Single(Assert.Single(conversation.Appended)).CallId);
        Assert.Single(invoker.Invoked);
    }

    [Fact]
    public async Task Without_a_budget_the_loop_runs_until_the_model_replies()
    {
        Conversation conversation = new(
            Calls(("call-1", "GetLocalTime")),
            Calls(("call-2", "GetLocalTime")),
            new TurnReply([]));

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, new RecordingInvoker(), null, "开始", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal(3, conversation.Asked.Count);
        Assert.Equal(2, conversation.Appended.Count);
    }

    [Fact]
    public async Task Budget_exhaustion_reports_the_limit()
    {
        Conversation conversation = new(Calls(("call-1", "GetLocalTime")));

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, new RecordingInvoker(), new TurnBudget(1), "开始", CancellationToken.None);

        Assert.True(outcome.IsError);
        Assert.Equal("Execution.TurnLimit", outcome.FirstError.Code);
        Assert.Contains("已向模型发起 1 次请求", outcome.FirstError.Description);
        Assert.Contains("上限 1 次", outcome.FirstError.Description);
        Assert.Single(conversation.Asked);
        Assert.Single(conversation.Appended);
    }

    [Fact]
    public async Task The_last_request_within_the_budget_can_reply()
    {
        Conversation conversation = new(Calls(("call-1", "GetLocalTime")), new TurnReply([]));

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, new RecordingInvoker(), new TurnBudget(2), "开始", CancellationToken.None);

        Assert.False(outcome.IsError);
        Assert.Equal(2, conversation.Asked.Count);
        Assert.Single(conversation.Appended);
    }

    [Fact]
    public async Task A_failed_request_stops_the_loop()
    {
        Conversation conversation = new(Error.Failure("Model.Failed", "模型失败"));
        RecordingInvoker invoker = new();

        ErrorOr<Success> outcome = await ExecutionRunner.TurnLoop(
            conversation, invoker, new TurnBudget(5), "开始", CancellationToken.None);

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