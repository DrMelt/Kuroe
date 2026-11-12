using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Tasks;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的任务表现。</summary>
public sealed class DialogueFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowContext task) =>
        DialogueView.Build(DialogueFlowAccess.State(task), task.Control, task);

    /// <inheritdoc/>
    public TaskSnapshot CreateSnapshot(ITaskFlowContext task)
    {
        DialogueFlowState state = DialogueFlowAccess.State(task);
        IFlowControl? control = task.Control;

        return new TaskSnapshot(
            task.Id,
            task.Title,
            task.Inputs,
            DialogueView.Build(state, control, task, task.State),
            task.State,
            task.Executions.Count(execution => execution.IsLive),
            task.JournalEntries,
            task.DroppedJournal,
            task.CreatedAt,
            task.LastActivityAt)
        {
            Executions = [.. task.Executions.Select(execution => execution.Snapshot())],
            Pending = control?.Pending() ?? [],
            StartTexts = new PortTexts(state.StartTexts.Texts),
        };
    }
}