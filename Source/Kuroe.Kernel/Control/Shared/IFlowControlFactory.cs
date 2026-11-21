namespace Kuroe.Kernel.Control.Shared;

/// <summary>为任务建立控制实例。</summary>
public interface IFlowControlFactory
{
    /// <summary>为任务建立控制实例。要求持有 <see cref="ITaskFlowContext.Gate"/>。</summary>
    IFlowControl CreateControl(ITaskFlowContext task);
}