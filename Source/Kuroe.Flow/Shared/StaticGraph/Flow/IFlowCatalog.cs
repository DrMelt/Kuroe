using ErrorOr;

namespace Kuroe.Flow.Shared.StaticGraph.Flow;

/// <summary>运行期的流程模板查询。由宿主实现，引擎只经它取流程定义。</summary>
public interface IFlowCatalog
{
    /// <summary>全部流程，按文件顺序。</summary>
    IReadOnlyList<FlowDefinition> All();

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    ErrorOr<FlowDefinition> Find(FlowName name);
}
