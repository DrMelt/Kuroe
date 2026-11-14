using ErrorOr;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>流程模板来源。由流程实现提供，宿主目录只聚合来源。</summary>
public interface IFlowTemplateSource
{
    /// <summary>全部流程模板，按实现内部顺序。</summary>
    IReadOnlyList<FlowTemplateView> All();

    /// <summary>按流程名取模板，不存在时返回错误。</summary>
    ErrorOr<FlowTemplateView> Find(FlowName name);
}