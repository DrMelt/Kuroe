using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control;

/// <summary>流程运行状态的标记。由具体流程实现解释。</summary>
public interface IFlowState
{
    /// <summary>提交时的开始输入。</summary>
    StartInputs Inputs { get; }
}