using ErrorOr;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>执行器对一段对话的使用面：发起一轮请求，读取本次对话的模型请求用量，并留下可恢复数据。</summary>
internal interface IConversation
{
    /// <summary>本次对话的模型请求用量与最近一次请求的上下文。</summary>
    ExecutionUsage Usage { get; }

    /// <summary>本次执行是否从恢复数据开始。</summary>
    bool IsRestored { get; }

    /// <summary>发起一轮请求，input 不为 null 时作为本轮输入写入历史。</summary>
    Task<ErrorOr<Success>> AskAsync(string? input, CancellationToken cancellationToken);

    /// <summary>生成本次执行可恢复的数据，不产生时为空。</summary>
    Task<LlmAgentSessionPayload?> CreateRestoreDataAsync(CancellationToken cancellationToken);

    /// <summary>丢弃尚未收口的一轮。</summary>
    void DiscardRound();
}