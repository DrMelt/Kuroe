using Microsoft.Extensions.AI;

namespace Kuroe.Ability.Runner.Llm.Sessions;

/// <summary>恢复会话装配使用的客户端。会话历史读取不发起请求，调用到这里表示装配错误。</summary>
internal sealed class UnavailableChatClient : IChatClient
{
    /// <inheritdoc/>
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("会话历史读取不应发起模型请求。");

    /// <inheritdoc/>
    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("会话历史读取不应发起模型请求。");

    /// <inheritdoc/>
    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}