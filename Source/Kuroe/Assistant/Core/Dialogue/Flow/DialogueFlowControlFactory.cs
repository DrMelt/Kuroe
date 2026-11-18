using Kuroe.Control.Shared;
using Kuroe.Input.Shared;
using Kuroe.Execution.Shared.Runtime;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>为对话任务建立控制实例。</summary>
internal sealed class DialogueFlowControlFactory(
    ITaskExecutionGateway gateway,
    IInputChannel inputs,
    IExecutionRunnerFactory runnerFactory) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task) =>
        new DialogueFlowControl(task, gateway, inputs, runnerFactory, DialogueFlowState.Of(task));
}