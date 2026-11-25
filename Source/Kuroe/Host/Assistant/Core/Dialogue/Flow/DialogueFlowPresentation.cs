using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Presentation.Shared;

namespace Kuroe.Host.Assistant.Dialogue.Flow;

/// <summary>对话流程的任务表现。</summary>
internal sealed class DialogueFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowView task) =>
        DialogueFlowView.Build(task, task.State);
}