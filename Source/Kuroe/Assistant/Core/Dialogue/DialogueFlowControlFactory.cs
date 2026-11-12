using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;

namespace Kuroe.Assistant.Dialogue;

/// <summary>为对话任务建立控制实例。</summary>
public sealed class DialogueFlowControlFactory(
    IExecutionRuntime executions,
    IFlowExecutionGateway gateway) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task) =>
        new DialogueFlowControl(task, executions, gateway, DialogueFlowAccess.State(task));
}