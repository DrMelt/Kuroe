namespace Kuroe.Control.Shared;

/// <summary>把流程运行期接到任务。存档恢复的节点、端口与中断执行在此重建。</summary>
public interface IFlowStateBinder
{
    /// <summary>绑定任务状态。任务构造时调用一次，此时控制实例尚未建立。</summary>
    void Bind(ITaskFlowContext task);
}