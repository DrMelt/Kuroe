using Kuroe.Assistant.Flows;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的存档捕获。</summary>
public sealed class DialogueFlowRecordSource : IFlowRecordSource
{
    /// <inheritdoc/>
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        DialogueFlowState state = DialogueFlowAccess.State(task);

        return new FlowRecord(
            FlowRecordKinds.Code,
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, DialogueFlowAccess.Values(state.Language)),
            new CodeFlowStateRecord(state.Inputs, state.StartTexts, DialogueFlowAccess.Values(state)));
    }

    /// <inheritdoc/>
    public IReadOnlyList<ExecutionRecord> CaptureExecutions(ITaskFlowContext task) =>
        [.. task.Executions.Select(execution => execution.Snapshot().CaptureRecord())];
}