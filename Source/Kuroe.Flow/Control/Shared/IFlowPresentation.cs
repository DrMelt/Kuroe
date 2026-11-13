using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control;

/// <summary>流程的任务表现：流程视图与等待步骤。</summary>
public interface IFlowPresentation
{
    /// <summary>当前流程视图，控制实例不存在时使用。</summary>
    IFlowView CaptureView(ITaskFlowContext task);

    /// <summary>此刻等待外部输入的步骤。</summary>
    IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task);
}