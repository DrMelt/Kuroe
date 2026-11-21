using Kuroe.Kernel.Control.Shared;

namespace Kuroe.Flow.Control.Runtime.Graph;

/// <summary>图流程状态的只读执行面：按提交顺序给出图侧执行对象。</summary>
public interface IGraphFlowState : IFlowState
{
    /// <summary>图运行期的执行，按提交顺序。</summary>
    IReadOnlyList<GraphExecution> Executions { get; }
}