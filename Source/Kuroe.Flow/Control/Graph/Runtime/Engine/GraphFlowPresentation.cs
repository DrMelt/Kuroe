using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime.Graph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>图流程的任务表现：从同一份中性执行状态生成流程视图与任务快照。</summary>
public sealed class GraphFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowContext task) => View(task);

    /// <inheritdoc/>
    public TaskSnapshot CreateSnapshot(ITaskFlowContext task)
    {
        GraphFlowState state = State(task);
        TaskRuntime runtime = state.Runtime;
        TaskProjection projection = new(state.Flow.Graph, runtime.Materializer, runtime.StaticPorts);
        IReadOnlyList<GraphExecutionSnapshot> executionSnapshots = GraphExecutions(task, state);

        return new TaskSnapshot(
            task.Id,
            task.Title,
            task.Inputs,
            projection.View(state.Flow.Model, executionSnapshots),
            task.State,
            task.Executions.Count(execution => execution.IsLive),
            task.JournalEntries,
            task.DroppedJournal,
            task.CreatedAt,
            task.LastActivityAt)
        {
            Executions = [.. task.Executions.Select(execution => execution.Snapshot())],
            Pending = [.. (task.Control?.Pending() ?? []).Select(PendingOf)],
            StartTexts = new FlowTexts(state.StartTexts.Texts),
        };
    }

    private static PendingStepView PendingOf(FlowPendingStep pending) => new(
        pending.Step.Value,
        pending.Kind == FlowPendingKind.Approve ? PendingStepKind.Approve : PendingStepKind.Input,
        pending.Question);

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
            state.Context(execution.Id).ControlVersion,
            state.ExecutionsRuntime ?? throw new InvalidOperationException("图流程没有执行运行期。")).Snapshot())];

    private static GraphFlowState State(ITaskFlowContext task)
    {
        if (task.FlowState is not GraphFlowState state || !state.Attached)
        {
            throw new InvalidOperationException("图流程状态还没有运行期。");
        }

        return state;
    }
}