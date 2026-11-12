using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>按存档流程记录与流程名分派已注册的流程实现。</summary>
public sealed class FlowFrameworkSource : IFlowFrameworkSource
{
    private readonly List<FlowProvider> _providers;

    /// <summary>按给定流程实现建立分派表。</summary>
    public FlowFrameworkSource(IEnumerable<FlowProvider> providers) => _providers = [.. providers];

    /// <inheritdoc/>
    public IReadOnlyList<FlowProvider> All() => _providers;

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Find(FlowRecord record)
    {
        FlowProvider[] found =
            [.. _providers.Where(provider => provider.Framework.Handles(record))];

        return found.Length switch
        {
            1 => found[0],
            0 => Error.Failure(
                FlowErrorCodes.RecordFormat,
                $"没有流程实现负责种类为 {record.Kind} 的存档流程。"),
            _ => Error.Failure(
                FlowErrorCodes.RecordFormat,
                $"多个流程实现同时负责种类为 {record.Kind} 的存档流程。"),
        };
    }

    /// <inheritdoc/>
    public ErrorOr<FlowProvider> Resolve(FlowName name)
    {
        FlowProvider[] found =
            [.. _providers.Where(provider => provider.Framework.Handles(name))];

        return found.Length switch
        {
            1 => found[0],
            0 => FlowErrors.FlowNotFound(name.Value),
            _ => Error.Failure(FlowErrorCodes.FlowName, $"多个流程实现同时负责流程 {name.Value}。"),
        };
    }
}