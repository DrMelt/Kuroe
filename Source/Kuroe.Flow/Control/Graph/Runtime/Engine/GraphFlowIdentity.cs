using Kuroe.Flow.Control.Graph.Runtime.Catalog;
using Kuroe.Control.Shared;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>图流程的身份：流程种类、实现标识与流程归属。</summary>
public sealed class GraphFlowIdentity(IFlowRuntimeCatalog flows) : IFlowIdentity
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