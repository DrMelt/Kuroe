using ErrorOr;
using Kuroe.Flow.Catalog.Shared;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Library;

/// <summary>流程库的只读查询：展开后的流程定义与编译结果。</summary>
public sealed class FlowCatalog(FlowLibrary library) : IFlowCatalog, IFlowRuntimeCatalog
{
    /// <summary>全部流程，按文件顺序。</summary>
    public IReadOnlyList<FlowDefinition> All() =>
        [.. library.Current.Compiled.Select(flow => flow.Definition)];

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    public ErrorOr<FlowDefinition> Find(FlowName name)
    {
        ErrorOr<FlowGraph> flow = FindCompiled(name);

        return flow.IsError ? flow.ErrorsOrEmptyList : flow.Value.Definition;
    }

    /// <summary>按名取编译完成的流程，不存在时返回错误。</summary>
    public ErrorOr<FlowGraph> FindCompiled(FlowName name)
    {
        FlowLibraryState state = library.Current;
        FlowGraph? flow = state.Compiled.FirstOrDefault(candidate => candidate.Definition.Name == name);
        if (flow is not null)
        {
            return flow;
        }

        FlowFailure? failure = state.Failures.FirstOrDefault(candidate => candidate.Name == name);

        return failure is null ? [FlowErrors.FlowNotFound(name.Value)] : [.. failure.Errors];
    }
}
