using Kuroe.Control.Shared;
using Kuroe.Presentation.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的任务表现。</summary>
public sealed class DialogueFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowView task) =>
        DialogueView.Build(task, task.State);
}