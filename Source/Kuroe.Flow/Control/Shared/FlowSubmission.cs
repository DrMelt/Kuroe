using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;

namespace Kuroe.Flow.Control;

/// <summary>一次提交准备好的流程状态。</summary>
public sealed record FlowSubmission(
    IFlowState State,
    string Title,
    FlowTexts StartTexts,
    IFlowDefinitionRecord? Definition);