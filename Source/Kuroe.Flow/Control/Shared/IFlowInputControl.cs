using ErrorOr;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Shared;

/// <summary>流程控制的等待输入面。只有需要外部输入的流程实现才提供。</summary>
public interface IFlowInputControl
{
    /// <summary>此刻等待外部输入的步骤。</summary>
    IReadOnlyList<FlowPendingStep> Pending();

    /// <summary>向等待中的步骤交付一次外部输入。</summary>
    ErrorOr<Success> Deliver(FlowStepPath step, string text);
}