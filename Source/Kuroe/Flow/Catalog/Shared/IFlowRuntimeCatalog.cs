using ErrorOr;
using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Flow.Catalog.Shared;

/// <summary>运行期的编译结果查询：提交任务直接取已校验的流程与执行视图。</summary>
public interface IFlowRuntimeCatalog
{
    /// <summary>按名取编译完成的流程，不存在时返回错误。</summary>
    ErrorOr<FlowGraph> FindCompiled(FlowName name);
}