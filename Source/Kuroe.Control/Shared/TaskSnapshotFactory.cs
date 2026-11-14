using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>任务的公共快照装配：任务身份与状态、执行、过程记录、等待步骤和开始文本。</summary>
public static class TaskSnapshotFactory
{
    /// <summary>按任务当前状态、流程视图与等待步骤装配快照。</summary>
    public static TaskSnapshot Create(
        ITaskFlowContext task,
        IFlowView view,
        IReadOnlyList<FlowPendingStep> pending,
        FlowTexts startTexts) =>
        new(
            task.Id,
            task.Title,
            task.Inputs,
            view,
            task.State,
            task.Executions.Count(execution => execution.IsLive),
            task.JournalEntries,
            task.DroppedJournal,
            task.CreatedAt,
            task.LastActivityAt)
        {
            Executions = [.. task.Executions.Select(execution => execution.Snapshot())],
            Pending = [.. pending.Select(PendingOf)],
            StartTexts = startTexts,
        };

    private static PendingStepView PendingOf(FlowPendingStep pending) => new(
        pending.Step.Value,
        pending.Kind == FlowPendingKind.Approve ? PendingStepKind.Approve : PendingStepKind.Input,
        pending.Question);
}