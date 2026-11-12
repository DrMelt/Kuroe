using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Runtime.Tasks;
using ExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>为一个任务建立流程控制实例。由具体流程驱动实现。</summary>
public interface IFlowControlFactory
{
    /// <summary>建立控制实例，尚未启动推进循环。</summary>
    IFlowControl Create(WorkTask task);

    /// <summary>没有活跃控制实例时停止一个已启动的执行。</summary>
    ErrorOr<Success> StopExecution(WorkTask task, ExecutionId id);
}