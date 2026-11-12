using ErrorOr;
using Kuroe.Flow.Execution;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>执行器对一段对话的使用面：发起一轮请求，读取本次对话的模型请求用量，并把工具结果写回历史。</summary>
internal interface IConversation
{
    /// <summary>本次对话的模型请求用量与最近一次请求的上下文。</summary>
    ExecutionUsage Usage { get; }

    /// <summary>发起一轮请求，input 非空时作为本轮输入写入历史。</summary>
    Task<ErrorOr<TurnReply>> AskAsync(string? input, CancellationToken cancellationToken);

    /// <summary>把工具结果写回历史，作为下一次请求的工具消息。</summary>
    void AppendResults(IReadOnlyList<FunctionResultContent> results);
}

/// <summary>一轮模型回复中的工具调用，空集合表示该轮已给出回复。</summary>
internal sealed record TurnReply(IReadOnlyList<FunctionCallContent> Calls);