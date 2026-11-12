namespace Kuroe.Flow.Control;

/// <summary>为任务建立控制实例。</summary>
public interface IFlowControlFactory
{
    /// <summary>为任务建立控制实例。要求持有任务 Gate。</summary>
    IFlowControl CreateControl(ITaskFlowContext task);
}