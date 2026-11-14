using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>按流程身份与流程名分派已注册的流程实现。</summary>
/// <remarks>按给定流程实现建立分派表。</remarks>
public sealed class FlowProviderSource(IEnumerable<FlowProvider> providers) : IFlowProviderSource
{
    private readonly List<FlowProvider> _providers = [.. providers];

    /// <inheritdoc/>
    public IReadOnlyList<FlowProvider> All() => _providers;

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Find(FlowIdentity identity)
    {
        FlowProvider[] found =
            [.. _providers.Where(provider => provider.Identity.Handles(identity))];

        return found.Length switch
        {
            1 => found[0],
            0 => Error.Failure(
                FlowErrorCodes.RecordFormat,
                $"没有流程实现负责 {identity.Kind}/{identity.Implementation} 的流程 {identity.Name}。"),
            _ => Error.Failure(
                FlowErrorCodes.RecordFormat,
                $"多个流程实现同时负责 {identity.Kind}/{identity.Implementation} 的流程 {identity.Name}。"),
        };
    }

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Resolve(FlowName name)
    {
        FlowProvider[] found =
            [.. _providers.Where(provider => provider.Identity.Handles(name))];

        return found.Length switch
        {
            1 => found[0],
            0 => FlowErrors.FlowNotFound(name.Value),
            _ => Error.Failure(FlowErrorCodes.FlowName, $"多个流程实现同时负责流程 {name.Value}。"),
        };
    }
}