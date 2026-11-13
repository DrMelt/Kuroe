using Kuroe.Flow.Presentation;
using Kuroe.Flow.Tasks;

namespace Kuroe.Flow.Control;

/// <summary>一个流程实现提供的端口集合。调用方只取需要的端口。</summary>
public sealed record FlowProvider(
    IFlowIdentity Identity,
    IFlowSubmission Submission,
    IFlowStateBinder StateBinder,
    IFlowControlFactory Controls,
    IFlowPresentation Presentation,
    IFlowRecordSource Records,
    IFlowTemplateSource Templates);