using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Control.Graph;
using Kuroe.Flow.Control.Graph.Tasks;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control.Graph.Engine;

/// <summary>图流程的任务表现：从同一份中性执行状态生成流程视图与等待步骤。</summary>
public sealed class GraphFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowContext task) => View(task);

    /// <inheritdoc/>
    public IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task) =>
        (task.Control as IFlowInputControl)?.Pending() ?? [];

    private static IFlowView View(ITaskFlowContext task)
    {
        GraphFlowState state = State(task);
        TaskRuntime runtime = state.Runtime;
        TaskProjection projection = new(state.Flow.Graph, runtime.Materializer, runtime.StaticPorts);

        return projection.View(state.Flow.Model, GraphExecutions(task, state));
    }

    private static IReadOnlyList<GraphExecutionSnapshot> GraphExecutions(
        ITaskFlowContext task,
        GraphFlowState state) =>
        [.. task.Executions.Select(execution => new GraphExecution(
            state.Context(execution.Id).Input,
            execution,
            state.Context(execution.Id).ControlVersion).Snapshot())];

    private static GraphFlowState State(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state || !state.Attached)
        {
            throw new InvalidOperationException("图流程状态还没有运行期。");
        }

        return state;
    }
}