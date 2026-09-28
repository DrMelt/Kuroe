namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程图上展平后的容器：直接成员执行节点与直接子容器的组织单元，自身不执行。
/// 组是汇合点：成员产出齐备时组可被上下游消费，门控决定届时是否停人等批准。</summary>
public sealed record FlowGroup(
    int Index,
    string Name,
    string Path,
    NodeGate Gate,
    IReadOnlyList<int> Members,
    IReadOnlyList<int> SubGroups) : GraphNode(Index, Name, Path, Gate);