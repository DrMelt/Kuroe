using ApiHub.Shared.Models;
using ErrorOr;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>单回合模型请求的边界：给定完整消息与请求选项，取回本轮完整回复。</summary>
internal interface ITurnClient
{
    /// <summary>发起一次请求，取回本轮完整回复。</summary>
    Task<ChatResponse> RespondAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken);
}

/// <summary>按模型解析单回合请求，接入信息与客户端缓存由实现负责。</summary>
internal interface ITurnClientSource
{
    /// <summary>解析该模型的请求边界，模型不可接入时返回错误。</summary>
    ErrorOr<ITurnClient> Resolve(ModelName model);
}

/// <summary>以模型客户端承载的单回合请求。</summary>
internal sealed class ChatTurnClient(IChatClient client) : ITurnClient
{
    /// <summary>承载请求的模型客户端。</summary>
    internal IChatClient Client => client;

    public Task<ChatResponse> RespondAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken) =>
        client.GetResponseAsync(messages, options, cancellationToken);
}