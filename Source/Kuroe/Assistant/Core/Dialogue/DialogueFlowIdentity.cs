using Kuroe.Assistant.Flows;
using Kuroe.Control.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Assistant.Dialogue;

/// <summary>代码定义对话流程的身份。</summary>
public sealed class DialogueFlowIdentity : IFlowIdentity
{
    /// <inheritdoc/>
    public string DisplayName => "代码流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => name == DialogueFlow.Name;

    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) =>
        identity.Kind == FlowRecordKinds.Code && identity.Implementation == DialogueFlow.Implementation;
}