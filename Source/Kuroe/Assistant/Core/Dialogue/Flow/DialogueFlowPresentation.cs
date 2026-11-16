using Kuroe.Control.Shared;
using Kuroe.Presentation.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程的任务表现。</summary>
internal sealed class DialogueFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowView task) =>
        DialogueFlowView.Build(task, task.State);
}