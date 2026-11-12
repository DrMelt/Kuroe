using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using ExecutionId = Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>为图驱动任务建立推进循环。</summary>
public sealed class GraphFlowControlFactory(
    ExecutionRuntime runtime,
    IExecutionTargetResolver targetResolver) : IFlowControlFactory
{
    /// <inheritdoc/>
    public IFlowControl Create(WorkTask task) => new TaskDriver(task, runtime, targetResolver);

    /// <inheritdoc/>
    public ErrorOr<Success> StopExecution(WorkTask task, ExecutionId id) => ExecutionStop.Stop(task, id);
}