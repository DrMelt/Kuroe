using ErrorOr;

namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>运行期的流程模板查询。由宿主实现，引擎只经它取流程定义。</summary>
public interface IFlowCatalog
{
    /// <summary>提交任务未指定流程时用的流程名，未设置时没有默认流程。</summary>
    FlowName? DefaultName { get; }

    /// <summary>按名取展开后的流程，不存在时返回错误。</summary>
    ErrorOr<FlowDefinition> Find(FlowName name);

    /// <summary>提交任务未指定流程时用的那条流程，未设置默认流程时返回错误。</summary>
    ErrorOr<FlowDefinition> Default();
}
