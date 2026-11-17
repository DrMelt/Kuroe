using ErrorOr;
using Kuroe.Execution.Shared.Conversation;
using Kuroe.Execution.Shared.Restore;

namespace Kuroe.Runner.Llm.Sessions;

/// <summary>Llm 会话历史的读取入口。实现由会话装配承担，读取不接触模型客户端。</summary>
public sealed class ExecutionConversationReader : IExecutionConversationReader
{
    /// <inheritdoc/>
    public Task<ErrorOr<ExecutionConversation>> ReadAsync(
        ExecutionRestoreState restore,
        CancellationToken cancellationToken = default) =>
        SessionFactory.ReadConversationAsync(restore, cancellationToken);
}