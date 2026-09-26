using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit;

namespace Kuroe.Tests;

/// <summary>会话由框架承载后依赖的历史契约：每轮只交本轮的输入，历史由会话合并；
/// 重铺按 SetInMemoryChatHistory 整体替换。</summary>
public sealed class AgentSessionFrameworkContractTests
{
    [Fact]
    public async Task History_accumulates_across_runs_when_input_carries_only_the_latest_message()
    {
        var client = new RecordingChatClient();
        ChatClientAgent agent = (ChatClientAgent)client.AsAIAgent();
        AgentSession session = await agent.CreateSessionAsync();

        session.SetInMemoryChatHistory([new ChatMessage(ChatRole.System, "你是一个助手。")]);

        await ConsumeAsync(agent.RunStreamingAsync("第一问", session));
        await ConsumeAsync(agent.RunStreamingAsync("第二问", session));

        IReadOnlyList<ChatMessage> last = client.Requests[^1];
        Assert.Equal(["你是一个助手。", "第一问", "回复", "第二问"], last.Select(message => message.Text));
    }

    [Fact]
    public async Task Relayout_replaces_session_history()
    {
        var client = new RecordingChatClient();
        ChatClientAgent agent = (ChatClientAgent)client.AsAIAgent();
        AgentSession session = await agent.CreateSessionAsync();

        await ConsumeAsync(agent.RunStreamingAsync("第一问", session));
        session.SetInMemoryChatHistory([new ChatMessage(ChatRole.System, "新提示词。")]);
        await ConsumeAsync(agent.RunStreamingAsync("新一问", session));

        IReadOnlyList<ChatMessage> last = client.Requests[^1];
        Assert.Equal(["新提示词。", "新一问"], last.Select(message => message.Text));
    }

    private sealed class RecordingChatClient : IChatClient
    {
        public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add([.. chatMessages]);
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "回复")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Requests.Add([.. chatMessages]);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "回复");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private static async Task ConsumeAsync<T>(IAsyncEnumerable<T> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }
}