using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Node.Llm;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;

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
            .. task.Executions
                .Select((execution, index) => (Snapshot: execution.Snapshot(), Round: index + 1))
                .Select(entry => new FlowExecutionView(
                    entry.Snapshot.Id.Value,
                    entry.Snapshot.State,
                    entry.Snapshot.Progress,
                    [])
                {
                    Round = entry.Round,
                    CreatedAt = entry.Snapshot.CreatedAt,
                    StartedAt = entry.Snapshot.StartedAt,
                    FinishedAt = entry.Snapshot.FinishedAt,
                    Failures = entry.Snapshot.Failures,
                    HasRestoreData = entry.Snapshot.RestoreData is not null,
                    ContinuedFrom = entry.Snapshot.ContinuedFrom?.Value,
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
            [
                new FlowPortView(DialogueFlow.StartPort.Value, "Text", null),
                new FlowPortView(DialogueFlow.ModePort.Value, "Text", null),
                new FlowPortView(DialogueFlow.ModelPort.Value, "Text", null),
                new FlowPortView(DialogueFlow.LanguagePort.Value, "Text", null),
            ],
            [
                new FlowStepView(
                    DialogueFlow.ReplyLabel,
                    DialogueFlow.ReplyLabel,
                    LlmNodeTypes.TypeName,
                    stepState,
                    [new FlowPortView(DialogueFlow.InputPort.Value, "Text", null)],
                    [new FlowPortView("完成", "Signal", null, Fires: false)],
                    executions,
                    int.MaxValue),
            ],
            []);
    }
}