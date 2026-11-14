using Kuroe.Control.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Graph;

/// <summary>图流程状态的只读执行面：按提交顺序给出图侧执行对象。宿主只读取，不修改图状态。</summary>
public interface IGraphFlowState : IFlowState
{
    /// <summary>图运行期的执行，按提交顺序。</summary>
    IReadOnlyList<GraphExecution> Executions { get; }
}