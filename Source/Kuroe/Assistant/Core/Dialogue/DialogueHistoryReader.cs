using ErrorOr;
using Kuroe.Kernel.Tasks.Runtime;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.Conversation;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Assistant.Dialogue;

/// <summary>会话历史的读取：基础历史取最后一次可读的恢复数据，其后的执行作为尾部返回。</summary>
internal sealed class DialogueHistoryReader(
    TaskRegistry registry,
    IExecutionConversationReader conversations)
{
    /// <summary>读取会话历史。恢复数据不可读时记录错误并从前一份继续，尾部仍按执行记录给出。</summary>
    internal async Task<ErrorOr<DialogueHistory>> ReadAsync(TaskId id, CancellationToken cancellationToken)
    {
        ErrorOr<WorkTask> found = registry.Find(id);
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        WorkTask task = found.Value;
        ExecutionSnapshot[] executions = task.UnderLock<ExecutionSnapshot[]>(
            () => [.. task.Executions.Select(execution => execution.Snapshot())]);

        List<Error> errors = [];
        ExecutionConversation conversation = ExecutionConversation.Empty;
        int baseIndex = -1;
        for (int index = executions.Length - 1; index >= 0; index--)
        {
            if (executions[index].RestoreData is not { } restore)
            {
                continue;
            }

            ErrorOr<ExecutionConversation> read = await conversations
                .ReadAsync(restore, cancellationToken)
                .ConfigureAwait(false);
            if (read.IsError)
            {
                errors.AddRange(read.ErrorsOrEmptyList);
                continue;
            }

            conversation = read.Value;
            baseIndex = index;
            break;
        }

        IReadOnlyList<ExecutionSnapshot> tail = baseIndex < 0
            ? executions
            : [.. executions.Skip(baseIndex + 1)];

        return new DialogueHistory(conversation, tail, errors);
    }
}