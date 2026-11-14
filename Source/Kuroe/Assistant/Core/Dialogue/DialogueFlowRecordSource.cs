using Kuroe.Assistant.Flows;
using Kuroe.Control.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>对话流程的存档捕获。</summary>
public sealed class DialogueFlowRecordSource : IFlowRecordSource
{
    /// <inheritdoc/>
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        DialogueFlowState state = DialogueFlowAccess.State(task);

        return new FlowRecord(
            new FlowIdentity(DialogueFlow.Name, FlowRecordKinds.Code, DialogueFlow.Implementation),
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, DialogueFlowAccess.Values(state.Language)),
            new CodeFlowStateRecord(state.Inputs, state.StartTexts, DialogueFlowAccess.Values(state)));
    }
}