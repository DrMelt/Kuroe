using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control;

/// <summary>流程的任务展示：流程视图与任务快照。</summary>
public interface IFlowPresentation
{
    /// <summary>当前流程视图，控制实例不存在时使用。</summary>
    IFlowView CaptureView(ITaskFlowContext task);

    /// <summary>任务快照，含流程视图、流程投影与中性执行列表。</summary>
    TaskSnapshot CreateSnapshot(ITaskFlowContext task);
}