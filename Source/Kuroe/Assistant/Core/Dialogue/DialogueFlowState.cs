using Kuroe.Assistant.Flows;
using Kuroe.Control.Shared;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;
using Kuroe.Node.Llm;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程状态：开始输入、工作区、模型与语言。</summary>
internal sealed class DialogueFlowState(
    StartInputs inputs,
    string workspace,
    string model,
    string language) : IFlowState
{
    /// <inheritdoc/>
    public StartInputs Inputs { get; } = inputs;

    /// <summary>工作区根目录。</summary>
    internal string Workspace { get; } = workspace;

    /// <summary>执行使用的模型名。</summary>
    internal string Model { get; } = model;

    /// <summary>回复语言。</summary>
    internal string Language { get; } = language;

    /// <summary>开始端口文本。</summary>
    internal FlowTexts StartTexts { get; } = new(
        inputs.Values.ToDictionary(input => input.Port, input => input.Text));

    /// <summary>等待输入的恢复种子。</summary>
    internal static FlowSeed AwaitingSeed => new([DialogueFlowControl.ReplyStep]);

    /// <summary>回话执行参数。</summary>
    internal LlmParameters Parameters => DialogueFlow.Parameters(Language);
}

/// <summary>对话流程的视图投影。</summary>
internal static class DialogueView
{
    internal static FlowView Build(
        IFlowInputControl? control,
        ITaskFlowContext task,
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

        FlowNodeState stepState;
        if (taskState is TaskState.Terminated or TaskState.Done)
        {
            stepState = FlowNodeState.Canceled;
        }
        else if (control?.Pending().Count > 0)
        {
            stepState = FlowNodeState.Pending;
        }
        else
        {
            stepState = task.Executions.Any(execution => execution.IsLive)
                ? FlowNodeState.Running
                : FlowNodeState.Done;
        }

        return new FlowView(
            DialogueFlow.Name.Value,
            FlowRecordKinds.Code,
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