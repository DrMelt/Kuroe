using Kuroe.Control.Shared;

namespace Kuroe.Maf.Control;

/// <summary>为 MAF 流程任务建立控制实例。</summary>
public sealed class MafFlowControlFactory(
    IFlowExecutionGateway gateway) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task)
    {
        MafFlowState state = MafFlowAccess.State(task);

        return new MafFlowControl(task, gateway, state, new MafCheckpointStore(task, state));
    }
}