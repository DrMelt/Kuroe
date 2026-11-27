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
    public IReadOnlyList<FlowTemplateView> FindAll(FlowName name) =>
        [.. All().Where(flow => flow.Name == name.Value)];

    /// <inheritdoc/>
    public ErrorOr<FlowTemplateView> Find(FlowName name, FlowImplementation implementation)
    {
        FlowTemplateView[] found =
        [
            .. All().Where(flow =>
                flow.Name == name.Value && flow.Implementation == implementation),
        ];

        return found.Length switch
        {
            1 => found[0],
            0 => FlowErrors.FlowNotFound(name.Value),
            _ => Error.Validation(
                FlowErrorCodes.FlowName,
                $"流程 {name.Value} 有多个 {implementation} 实现。"),
        };
    }
}