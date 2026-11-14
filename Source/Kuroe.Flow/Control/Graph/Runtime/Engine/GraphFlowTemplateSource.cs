using ErrorOr;
using Kuroe.Flow.Control.Graph.Runtime.Catalog;
using Kuroe.Control.Shared;
using Kuroe.Flow.Node.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Node.Shared.StaticGraph.Flow;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Graph.Runtime.Engine;

/// <summary>图流程的模板来源：把流程定义与宿主开始输入投影为中性模板。</summary>
public sealed class GraphFlowTemplateSource(
    IFlowCatalog flows,
    IHostStartInputs hostInputs) : IFlowTemplateSource
{
    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All() =>
        [.. flows.All().Select(Project)];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name)
    {
        ErrorOr<FlowDefinition> found = flows.Find(name);

        return found.IsError ? found.ErrorsOrEmptyList : Project(found.Value);
    }

    private FlowTemplateView Project(FlowDefinition flow) =>
        FlowTemplateProjection.Project(flow, hostInputs.Bindings(flow.Name));
}