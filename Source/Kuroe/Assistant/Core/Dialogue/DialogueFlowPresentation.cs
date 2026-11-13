using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的任务表现。</summary>
public sealed class DialogueFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowContext task) =>
        DialogueView.Build(
            DialogueFlowAccess.State(task),
            task.Control as IFlowInputControl,
            task,
            task.State);

    /// <inheritdoc/>
    public IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task) =>
        (task.Control as IFlowInputControl)?.Pending() ?? [];
}