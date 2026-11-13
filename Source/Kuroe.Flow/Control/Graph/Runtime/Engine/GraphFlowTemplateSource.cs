using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control.Graph.Engine;

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