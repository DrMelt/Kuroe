using Kuroe.Assistant.Flows;
using Kuroe.Flow.Control;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Assistant.Dialogue;

/// <summary>代码定义对话流程的身份。</summary>
public sealed class DialogueFlowIdentity : IFlowFramework
{
    /// <inheritdoc/>
    public string Kind => FlowRecordKinds.Code;

    /// <inheritdoc/>
    public string Name => "代码流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => name == DialogueFlow.Name;

    /// <inheritdoc/>
    public bool Handles(FlowRecord record) =>
        record.Kind == Kind
        && record.Definition is CodeFlowDefinitionRecord definition
        && definition.Implementation == DialogueFlow.Implementation;
}