using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Flow;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Runtime.Tasks;

/// <summary>按种类与流程名分派已注册的流程框架。</summary>
public sealed class FlowFrameworkSource : IFlowFrameworkSource
{
    private readonly List<IFlowFramework> _frameworks;

    /// <summary>按给定框架建立分派表。</summary>
    public FlowFrameworkSource(IEnumerable<IFlowFramework> frameworks) => _frameworks = [.. frameworks];

    /// <inheritdoc/>
    public IReadOnlyList<IFlowFramework> All() => _frameworks;

    /// <inheritdoc/>
    public ErrorOr<IFlowFramework> Find(string kind)
    {
        IFlowFramework? found = _frameworks.Find(
            framework => string.Equals(framework.Kind, kind, StringComparison.Ordinal));
        if (found is null)
        {
            return Error.Failure(FlowErrorCodes.RecordFormat, $"没有种类为 {kind} 的流程框架。");
        }

        return Ok(found);
    }

    /// <inheritdoc/>
    public ErrorOr<IFlowFramework> Resolve(FlowName name)
    {
        IFlowFramework? found = _frameworks.Find(framework => framework.Handles(name));
        if (found is null)
        {
            return FlowErrors.FlowNotFound(name.Value);
        }

        return Ok(found);
    }

    private static ErrorOr<T> Ok<T>(T value) where T : notnull => value;
}