using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Control.Shared;
using Kuroe.Node.Llm;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程的视图投影。</summary>
internal static class DialogueFlowView
{
    internal static FlowView Build(
        ITaskFlowView task,
        TaskState? taskState = null)
    {
        IReadOnlyList<FlowExecutionView> executions =
        [
            .. task.Executions.Select(execution => execution.Snapshot())
                .Select(snapshot => new FlowExecutionView(
                    snapshot.Id.Value,
                    snapshot.State,
                    snapshot.Progress,
                    [])
                {
                    Round = snapshot.Request.Attempt,
                    CreatedAt = snapshot.CreatedAt,
                    StartedAt = snapshot.StartedAt,
                    FinishedAt = snapshot.FinishedAt,
                    Failures = snapshot.Failures,
                    HasRestoreData = snapshot.RestoreData is not null,
                    ContinuedFrom = snapshot.ContinuedFrom?.Value,
                }),
        ];

        FlowItemState stepState;
        if (taskState is TaskState.Terminated or TaskState.Done)
        {
            stepState = FlowItemState.Canceled;
        }
        else if (task.Pending.Count > 0)
        {
            stepState = FlowItemState.Pending;
        }
        else
        {
            stepState = task.Executions.Any(execution => execution.IsLive)
                ? FlowItemState.Running
                : FlowItemState.Done;
        }

        return new FlowView(
            DialogueFlow.Name.Value,
            CodeFlowKind.Value,
            "内置对话：回答用户的每轮输入",
            [new FlowPortView(DialogueFlow.StartPort.Value, "Text", null)],
            [
                new FlowStepView(
                    DialogueFlow.ReplyLabel,
                    DialogueFlow.ReplyLabel,
                    LlmNodeTypes.Kind,
                    stepState,
                    [new FlowPortView(DialogueFlow.InputPort.Value, "Text", null)],
                    [new FlowPortView("完成", "Signal", null, Fires: false)],
                    executions,
                    int.MaxValue),
            ],
            []);
    }
}