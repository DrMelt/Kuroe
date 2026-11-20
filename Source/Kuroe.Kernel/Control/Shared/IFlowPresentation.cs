using Kuroe.Kernel.Presentation.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程的任务表现：流程视图。等待步骤由任务视图提供。</summary>
public interface IFlowPresentation
{
    /// <summary>当前流程视图，控制实例不存在时使用。</summary>
    IFlowView CaptureView(ITaskFlowView task);
}