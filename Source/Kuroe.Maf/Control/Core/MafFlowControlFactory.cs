using Kuroe.Kernel.Control.Shared;

namespace Kuroe.Maf.Control;

/// <summary>为 MAF 流程任务建立控制实例。</summary>
public sealed class MafFlowControlFactory(
    ITaskExecutionGateway gateway) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl CreateControl(ITaskFlowContext task) =>
        new MafFlowControl(task, gateway, MafFlowAccess.State(task));
}