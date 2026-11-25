using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Input.Shared;
using Kuroe.Kernel.Execution.Shared.Runtime;

namespace Kuroe.Host.Assistant.Dialogue.Flow;

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