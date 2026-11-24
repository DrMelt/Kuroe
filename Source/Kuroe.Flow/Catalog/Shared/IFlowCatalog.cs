using ErrorOr;
using Kuroe.FlowDescription.Definition.Shared.Flow;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Catalog.Shared;

/// <summary>运行期的流程模板查询。实现由流程库提供。</summary>
public interface IFlowCatalog
{
    /// <summary>全部流程，按文件顺序。</summary>
    IReadOnlyList<FlowDefinition> All();

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    ErrorOr<FlowDefinition> Find(FlowName name);
}