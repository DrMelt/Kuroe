using Kuroe.CodeFlow.Records.Shared;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Host.Assistant.Dialogue.Flow;

/// <summary>代码定义对话流程的身份。</summary>
internal sealed class DialogueFlowIdentity : IFlowIdentity
{
    /// <inheritdoc/>
    public string DisplayName => "代码流程";

    /// <inheritdoc/>
    public string Implementation => DialogueFlow.Implementation;

    /// <inheritdoc/>
    public bool Handles(FlowName name) => name == DialogueFlow.Name;

    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) =>
        identity.Kind == CodeFlowKind.Value && identity.Implementation == DialogueFlow.Implementation;
}