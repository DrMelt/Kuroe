using ErrorOr;
using Kuroe.Kernel.Presentation.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>流程模板的只读目录。</summary>
public interface IFlowTemplateCatalog
{
    /// <summary>全部流程模板，按流程库顺序。</summary>
    IReadOnlyList<FlowTemplateView> All();

    /// <summary>按流程名取模板，不存在时返回错误。</summary>
    ErrorOr<FlowTemplateView> Find(FlowName name);
}