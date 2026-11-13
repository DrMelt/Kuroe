using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;

namespace Kuroe.Assistant.Dialogue;

/// <summary>为对话任务建立控制实例。</summary>
public sealed class DialogueFlowControlFactory(
    IFlowExecutionGateway gateway) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task) =>
        new DialogueFlowControl(task, gateway, DialogueFlowAccess.State(task));
}