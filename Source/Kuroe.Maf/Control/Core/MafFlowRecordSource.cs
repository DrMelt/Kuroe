using Kuroe.Control.Shared;
using Kuroe.Maf.Records.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control;

/// <summary>MAF 流程的存档捕获：流程身份、定义摘要与运行期检查点。</summary>
public sealed class MafFlowRecordSource : IFlowRecordSource
{
    /// <inheritdoc/>
    public FlowRecord CaptureRecord(ITaskFlowContext task)
    {
        MafFlowState state = MafFlowAccess.State(task);
        MafCheckpoint? checkpoint = state.Checkpoint;
        string[] pending = task.Control is { } control
            ? [.. control.CaptureSeed().Steps.Select(step => step.Value)]
            : [.. state.PendingSteps];

        return new FlowRecord(
            new FlowIdentity(state.Definition.Name, MafFlowKind.Value, MafFlowIdentity.Implementation),
            new MafFlowDefinitionRecord(MafFlowIdentity.Implementation, state.Definition.Name.Value),
            new MafFlowStateRecord(
                state.Inputs,
                state.StartTexts,
                state.Model,
                checkpoint?.Session,
                checkpoint?.Id,
                checkpoint?.Data,
                pending));
    }
}