using Kuroe.Assistant.Flows;
using Kuroe.Flow.Control;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Assistant.Dialogue;

/// <summary>代码定义对话流程的身份。</summary>
public sealed class DialogueFlowIdentity : IFlowFramework
{
    /// <inheritdoc/>
    public string DisplayName => "代码流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => name == DialogueFlow.Name;

    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) =>
        identity.Kind == FlowRecordKinds.Code && identity.Implementation == DialogueFlow.Implementation;
}