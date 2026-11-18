using Kuroe.Flow.Control.Runtime.Graph;
using Kuroe.Control.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Flow.Control.Runtime.Tasks;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Control.Runtime.Engine;

/// <summary>图流程的任务表现：从同一份中性执行状态生成流程视图与等待步骤。</summary>
public sealed class GraphFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowView task) => View(task);

    private static IFlowView View(ITaskFlowView task)
    {
        GraphFlowState state = State(task);
        TaskRuntime runtime = state.Runtime;
        TaskProjection projection = new(state.Flow.Graph, runtime.Materializer, runtime.StaticPorts);

        return projection.View(state.Flow.Definition, GraphExecutions(task, state));
    }

    private static IReadOnlyList<GraphExecutionSnapshot> GraphExecutions(
        ITaskFlowView task,
        GraphFlowState state) =>
        [.. task.Executions.Select(execution => new GraphExecution(
            state.Context(execution.Id).Input,
            execution,
            state.Context(execution.Id).ControlVersion).Snapshot())];

    private static GraphFlowState State(ITaskFlowView task)
    {
        if (task.FlowState is not GraphFlowState state || !state.Attached)
        {
            throw new InvalidOperationException("图流程状态还没有运行期。");
        }

        return state;
    }
}