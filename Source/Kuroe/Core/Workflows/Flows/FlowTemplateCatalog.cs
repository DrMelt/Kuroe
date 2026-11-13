using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.StaticGraph.Flow;
using Kuroe.Flow.Tasks;

namespace Kuroe.Workflows.Flows;

/// <summary>把流程库的图定义投影为宿主可读的中性模板目录。</summary>
public sealed class FlowTemplateCatalog(
    IFlowCatalog flows,
    IHostStartInputs hostInputs) : IFlowTemplateCatalog
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