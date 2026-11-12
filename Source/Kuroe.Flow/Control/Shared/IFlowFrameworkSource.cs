using ErrorOr;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.StaticGraph.Flow;

namespace Kuroe.Flow.Control;

/// <summary>按种类与流程名查找流程实现。</summary>
public interface IFlowFrameworkSource
{
    /// <summary>全部已注册的流程实现。</summary>
    IReadOnlyList<FlowProvider> All();

    /// <summary>按种类取流程实现，不存在时返回错误。</summary>
    ErrorOr<FlowProvider> Find(string kind);

    /// <summary>按流程名取负责的实现，不存在时返回错误。</summary>
    ErrorOr<FlowProvider> Resolve(FlowName name);
}