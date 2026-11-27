using ErrorOr;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>按流程身份与流程名查找流程实现。</summary>
public interface IFlowProviderSource
{
    /// <summary>全部已注册的流程实现。</summary>
    IReadOnlyList<FlowProvider> All();

    /// <summary>按流程身份取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Find(FlowIdentity identity);

    /// <summary>按流程名取全部负责的实现，按注册顺序。</summary>
    IReadOnlyList<FlowProvider> FindAll(FlowName name);

    /// <summary>按流程名取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Resolve(FlowName name);

    /// <summary>按流程名与实现取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Resolve(FlowName name, FlowImplementation implementation);
}