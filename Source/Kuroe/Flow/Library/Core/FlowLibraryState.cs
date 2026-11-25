using Kuroe.Flow.Graph.Shared.StaticGraph;
using Kuroe.FlowDescription.Description.Shared;

namespace Kuroe.Flow.Library;

/// <summary>流程库状态：描述文档、编译结果与不可用流程。导入时整体替换。</summary>
internal sealed record FlowLibraryState(
    FlowDescriptionSet Document,
    IReadOnlyList<FlowGraph> Compiled,
    IReadOnlyList<FlowFailure> Failures);
