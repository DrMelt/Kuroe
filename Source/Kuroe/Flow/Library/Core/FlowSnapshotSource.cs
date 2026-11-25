using ErrorOr;
using Kuroe.Flow.Records.Shared;
using Kuroe.FlowDescription.Description.Shared;
using Kuroe.FlowDescription.Json.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Library;

/// <summary>按当前流程文档捕获存档快照文本。</summary>
public sealed class FlowSnapshotSource(
    FlowLibrary library,
    INodeFormatWriter nodes,
    IFlowFormatWriter flows) : IFlowSnapshotSource
{
    /// <inheritdoc/>
    public ErrorOr<GraphFlowDefinitionRecord> Capture(FlowName name)
    {
        FlowDescriptionSet document = library.Current.Document;
        FlowEntry? flow = document.Flows.FirstOrDefault(candidate => candidate.Name == name.Value);
        if (flow is null)
        {
            return [FlowErrors.FlowNotFound(name.Value)];
        }

        ErrorOr<string> nodeText = nodes.Write(document.Nodes);
        if (nodeText.IsError)
        {
            return nodeText.ErrorsOrEmptyList;
        }

        ErrorOr<string> text = flows.Write([flow]);

        return text.IsError ? text.ErrorsOrEmptyList : new GraphFlowDefinitionRecord(name, nodeText.Value, text.Value);
    }
}
