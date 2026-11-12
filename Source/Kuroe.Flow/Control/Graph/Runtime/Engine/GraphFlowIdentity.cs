using Kuroe.Flow.Control;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Runtime.Engine;

/// <summary>图流程的身份：存档种类、展示名与流程归属。</summary>
public sealed class GraphFlowIdentity(IFlowRuntimeCatalog flows) : IFlowFramework
{
    /// <inheritdoc/>
    public string Kind => FlowRecordKinds.Graph;

    /// <inheritdoc/>
    public string Name => "图流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => flows.FindCompiled(name).IsSuccess;

    /// <inheritdoc/>
    public bool Handles(FlowRecord record) =>
        record.Kind == Kind && record.Definition is GraphFlowDefinitionRecord;
}