using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Runtime;

/// <summary>流程模板目录：聚合各流程实现提供的模板来源。</summary>
public sealed class FlowTemplateCatalog(IEnumerable<IFlowTemplateSource> sources)
    : IFlowTemplateCatalog, IFlowTemplateRegistrar
{
    private readonly List<IFlowTemplateSource> _sources = [.. sources];

    /// <inheritdoc/>
    public void Register(IFlowTemplateSource source) => _sources.Add(source);

    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All() =>
        [.. _sources.SelectMany(source => source.All())];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name)
    {
        FlowTemplateView? found = _sources
            .Select(source => source.Find(name))
            .Where(result => result.IsSuccess)
            .Select(result => (FlowTemplateView?)result.Value)
            .FirstOrDefault();

        return found is null ? FlowErrors.FlowNotFound(name.Value) : found;
    }
}