namespace Kuroe.Flow.Control;

/// <summary>为任务建立流程运行期与控制实例。</summary>
public interface IFlowControlFactory
{
    /// <summary>把流程运行期接到任务，存档恢复的节点与端口在此重建。要求任务尚未建立控制。</summary>
    void Attach(ITaskFlowContext task);

    /// <summary>为任务建立控制实例。要求持有任务 Gate。</summary>
    IFlowControl CreateControl(ITaskFlowContext task);
}