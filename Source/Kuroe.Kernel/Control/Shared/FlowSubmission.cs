using Kuroe.Kernel.Tasks.Records.Shared;
using Kuroe.Kernel.Tasks.Shared;

namespace Kuroe.Kernel.Control.Shared;

/// <summary>一次提交准备好的流程状态。</summary>
public sealed record FlowSubmission(
    IFlowState State,
    string Title,
    FlowTexts StartTexts,
    IFlowDefinitionRecord? Definition);