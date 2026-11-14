using ErrorOr;
using Kuroe.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>按流程身份与流程名查找流程实现。</summary>
public interface IFlowProviderSource
{
    /// <summary>全部已注册的流程实现。</summary>
    IReadOnlyList<FlowProvider> All();

    /// <summary>按流程身份取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Find(FlowIdentity identity);

    /// <summary>按流程名取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Resolve(FlowName name);
}