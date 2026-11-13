using ErrorOr;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Flow.Control.Shared;

/// <summary>流程模板的只读目录，供宿主浏览流程与生成提交表单。</summary>
public interface IFlowTemplateCatalog
{
    /// <summary>全部流程模板，按流程库顺序。</summary>
    IReadOnlyList<FlowTemplateView> All();

    /// <summary>按流程名取模板，不存在时返回错误。</summary>
    ErrorOr<FlowTemplateView> Find(FlowName name);
}