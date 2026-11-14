using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Workflows.Flows;

/// <summary>流程模板目录：聚合各流程实现提供的模板来源，或直接包装单一来源。</summary>
public sealed class FlowTemplateCatalog : IFlowTemplateCatalog
{
    private readonly IFlowProviderSource? _providers;
    private readonly IFlowTemplateSource? _templates;

    /// <summary>按流程实现目录聚合模板。</summary>
    public FlowTemplateCatalog(IFlowProviderSource providers) => _providers = providers;

    /// <summary>直接使用一个模板来源。</summary>
    public FlowTemplateCatalog(IFlowTemplateSource templates) => _templates = templates;

    /// <inheritdoc/>
    public IReadOnlyList<FlowTemplateView> All()
    {
        if (_providers is { } providers)
        {
            return [.. providers.All().SelectMany(provider => provider.Templates.All())];
        }

        return Templates.All();
    }

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name)
    {
        if (_providers is { } providers)
        {
            ErrorOr<FlowProvider> resolved = providers.Resolve(name);

            return resolved.IsError ? resolved.ErrorsOrEmptyList : resolved.Value.Templates.Find(name);
        }

        return Templates.Find(name);
    }

    private IFlowTemplateSource Templates =>
        _templates ?? throw new InvalidOperationException("流程模板目录没有模板来源。");
}