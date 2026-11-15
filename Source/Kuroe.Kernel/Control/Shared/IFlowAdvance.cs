using Kuroe.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>流程控制的推进面：输入交付后按受影响步骤继续推进。只有需要外部输入的流程实现才提供。</summary>
public interface IFlowAdvance
{
    /// <summary>按已交付的步骤推进。要求持有任务 Gate 之外的调用。</summary>
    void Advance(IReadOnlyList<FlowStepPath> steps);
}