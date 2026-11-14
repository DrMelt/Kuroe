using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;

namespace Kuroe.Control.Shared;

/// <summary>一次提交准备好的流程状态。</summary>
public sealed record FlowSubmission(
    IFlowState State,
    string Title,
    FlowTexts StartTexts,
    IFlowDefinitionRecord? Definition);