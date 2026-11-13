using Kuroe.Flow.Control;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control.Graph.Engine;

/// <summary>图流程的身份：流程种类、实现标识与流程归属。</summary>
public sealed class GraphFlowIdentity(IFlowRuntimeCatalog flows) : IFlowFramework
{
    /// <summary>图流程的实现标识。</summary>
    public const string Implementation = "graph";

    /// <inheritdoc/>
    public string DisplayName => "图流程";

    /// <inheritdoc/>
    public bool Handles(FlowName name) => flows.FindCompiled(name).IsSuccess;

    /// <inheritdoc/>
    public bool Handles(FlowIdentity identity) =>
        identity.Kind == FlowRecordKinds.Graph && identity.Implementation == Implementation;
}