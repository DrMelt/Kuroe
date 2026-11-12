using ErrorOr;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control;

/// <summary>按存档流程记录与流程名查找流程实现。</summary>
public interface IFlowFrameworkSource
{
    /// <summary>全部已注册的流程实现。</summary>
    IReadOnlyList<FlowProvider> All();

    /// <summary>按存档流程记录取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Find(FlowRecord record);

    /// <summary>按流程名取负责的实现，不存在或存在多个时返回错误。</summary>
    ErrorOr<FlowProvider> Resolve(FlowName name);
}