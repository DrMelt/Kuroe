using ErrorOr;
using Kuroe.Execution.Shared.Restore;

namespace Kuroe.Execution.Shared.Conversation;

/// <summary>从执行恢复数据读取会话历史。读取不发起模型请求，不决定展示方式。</summary>
public interface IExecutionConversationReader
{
    /// <summary>读取一份恢复数据中的会话历史。格式不受支持或数据损坏时返回错误。</summary>
    Task<ErrorOr<ExecutionConversation>> ReadAsync(
        ExecutionRestoreState restore,
        CancellationToken cancellationToken = default);
}