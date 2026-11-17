using ErrorOr;
using Kuroe.Execution.Shared.Conversation;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Assistant.Dialogue;

/// <summary>一次会话历史的读取结果：基础历史来自最后一次可读的恢复数据，尾部是之后的执行。</summary>
public sealed record DialogueHistory(
    ExecutionConversation Conversation,
    IReadOnlyList<ExecutionSnapshot> Tail,
    IReadOnlyList<Error> Errors);