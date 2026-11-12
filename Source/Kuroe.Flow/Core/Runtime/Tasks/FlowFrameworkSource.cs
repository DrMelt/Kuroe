using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>按种类与流程名分派已注册的流程实现。</summary>
public sealed class FlowFrameworkSource : IFlowFrameworkSource
{
    private readonly List<FlowProvider> _providers;

    /// <summary>按给定流程实现建立分派表。</summary>
    public FlowFrameworkSource(IEnumerable<FlowProvider> providers) => _providers = [.. providers];

    /// <inheritdoc/>
    public IReadOnlyList<FlowProvider> All() => _providers;

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Find(string kind)
    {
        FlowProvider? found = _providers.Find(
            provider => string.Equals(provider.Framework.Kind, kind, StringComparison.Ordinal));
        if (found is null)
        {
            return Error.Failure(FlowErrorCodes.RecordFormat, $"没有种类为 {kind} 的流程实现。");
        }

        return Ok(found);
    }

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Resolve(FlowName name)
    {
        FlowProvider? found = _providers.Find(provider => provider.Framework.Handles(name));
        if (found is null)
        {
            return FlowErrors.FlowNotFound(name.Value);
        }

        return Ok(found);
    }

    private static ErrorOr<T> Ok<T>(T value) where T : notnull => value;
}