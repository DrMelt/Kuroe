using Kuroe.Execution.Shared;
using Kuroe.Node.Llm.Executions;
using Microsoft.Extensions.AI;

namespace Kuroe.Node.Llm.Sessions;

/// <summary>模型服务调用的观察层：统计请求用量，并把思考与回复写进过程记录。</summary>
internal sealed class ExecutionChatClient(IChatClient inner, ProcessJournal journal) : DelegatingChatClient(inner)
{
    private volatile ExecutionUsage _usage = ExecutionUsage.None;

    /// <summary>本次执行的模型请求用量与最近一次请求的上下文。</summary>
    internal ExecutionUsage Usage => _usage;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        TokenUsage? usage = null;
        _usage = _usage.BeginRequest();
        try
        {
            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            usage = response.Usage is { } details ? TokenUsageOf(details) : null;
            RecordContent(response, journal);

            return response;
        }
        finally
        {
            _usage = _usage.CompleteRequest(usage);
        }
    }

    /// <summary>把本轮回复中的思考与回复内容按种类写进过程记录，其余内容由会话历史承载。</summary>
    private static void RecordContent(ChatResponse response, ProcessJournal journal)
    {
        foreach (AIContent content in response.Messages.SelectMany(message => message.Contents))
        {
            switch (content)
            {
                case TextReasoningContent { Text.Length: > 0 } reasoning:
                    journal.AppendContent(ProcessContentKind.Reasoning, reasoning.Text);
                    break;

                case TextContent { Text.Length: > 0 } text:
                    journal.AppendContent(ProcessContentKind.Reply, text.Text);
                    break;
            }
        }
    }

    /// <summary>把模型侧用量转成执行侧用量，合计缺失时由输入与输出补齐。</summary>
    private static TokenUsage TokenUsageOf(UsageDetails details)
    {
        long? input = details.InputTokenCount;
        long? output = details.OutputTokenCount;

        return new TokenUsage(
            input,
            output,
            details.TotalTokenCount ?? (input is { } inputCount && output is { } outputCount
                ? inputCount + outputCount
                : null),
            details.CachedInputTokenCount,
            details.ReasoningTokenCount);
    }
}