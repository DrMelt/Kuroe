using Kuroe.Kernel.Control.Shared;
using Kuroe.Maf.Definition;
using Kuroe.Maf.Records.Shared;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的视图：步骤来自定义，状态与执行来自任务。</summary>
public sealed class MafFlowPresentation : IFlowPresentation
{
    /// <inheritdoc/>
    public IFlowView CaptureView(ITaskFlowView task) => View(task);

    private static FlowView View(ITaskFlowView task)
    {
        MafFlowState state = MafFlowAccess.State(task);

        return new FlowView(
            state.Definition.Name.Value,
            MafFlowKind.Value,
            state.Definition.Description,
            [.. state.Definition.StartPorts.Select(port =>
                MafPortView.Start(port, state.StartTexts.Text(port.Name)))],
            [.. state.Definition.Steps.Select(step => Step(step, task))],
            []);
    }

    private static FlowStepView Step(MafStep step, ITaskFlowView task)
    {
        IReadOnlyList<FlowExecutionView> executions =
        [
            .. task.Executions
                .Where(execution => execution.Request.Step == step.ExecutionStepId)
                .Select((execution, index) => Execution(execution, index + 1)),
        ];
        FlowExecutionView? last = executions.LastOrDefault(
            execution => execution.State is ExecutionState.Failed or ExecutionState.Interrupted);

        return new FlowStepView(
            step.Id.Value,
            step.Label,
            "Maf",
            StepState(task, executions),
            [],
            [.. step.OutputPorts().Select(MafPortView.Output)],
            executions,
            int.MaxValue)
        {
            Block = last is { Failures.Count: > 0 } ? last.Failures[0] : null,
        };
    }

    private static FlowExecutionView Execution(IExecution execution, int round)
    {
        ExecutionSnapshot snapshot = execution.Snapshot();

        return new FlowExecutionView(
            snapshot.Id.Value,
            snapshot.State,
            snapshot.Progress,
            [])
        {
            Round = round,
            CreatedAt = snapshot.CreatedAt,
            StartedAt = snapshot.StartedAt,
            FinishedAt = snapshot.FinishedAt,
            Failures = snapshot.Failures,
            HasRestoreData = snapshot.RestoreData is not null,
            ContinuedFrom = snapshot.ContinuedFrom?.Value,
        };
    }

    private static FlowItemState StepState(ITaskFlowView task, IReadOnlyList<FlowExecutionView> executions)
    {
        if (task.State == TaskState.Terminated)
        {
            return FlowItemState.Canceled;
        }

        if (executions.Any(execution => execution.State is ExecutionState.Queued or ExecutionState.Running))
        {
            return FlowItemState.Running;
        }

        if (executions.Any(execution => execution.State == ExecutionState.Succeeded))
        {
            return FlowItemState.Done;
        }

        if (executions.Any(execution => execution.State is ExecutionState.Failed or ExecutionState.Interrupted))
        {
            return FlowItemState.Blocked;
        }

        return FlowItemState.Pending;
    }
}