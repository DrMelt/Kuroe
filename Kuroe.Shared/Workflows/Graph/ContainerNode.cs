using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>流程图上展平后的容器：直接成员执行节点与直接子容器的组织单元，自身不执行。
/// 容器是汇合点：成员产出齐备时容器可被上下游消费，门控决定届时是否停人等批准。</summary>
public sealed record ContainerNode(
    int Index,
    NodeName Name,
    string Path,
    NodeGate Gate,
    IReadOnlyList<int> Members,
    IReadOnlyList<int> SubContainers) : GraphNode(Index, Name, Path, Gate);
