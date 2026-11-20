using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue.Flow;

/// <summary>对话流程的存档捕获。</summary>
internal sealed class DialogueFlowRecordSource : IFlowRecordSource
{
    /// <inheritdoc/>
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        DialogueFlowState state = DialogueFlowState.Of(task);

        return new FlowRecord(
            new FlowIdentity(DialogueFlow.Name, CodeFlowKind.Value, DialogueFlow.Implementation),
            new CodeFlowDefinitionRecord(DialogueFlow.Implementation, DialogueFlow.DefinitionValues(state.Language)),
            state.ToRecord());
    }
}