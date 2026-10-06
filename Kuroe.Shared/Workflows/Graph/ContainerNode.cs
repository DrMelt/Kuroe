using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>流程图上展平后的容器：直接成员执行节点与直接子容器的组织单元，自身不执行。
/// 容器是汇合点：成员产出齐备时容器可被上下游消费，门控决定届时是否停人等批准。</summary>
public sealed record ContainerNode : GraphNode
{
    /// <summary>直接成员执行节点，先根序。</summary>
    public required IReadOnlyList<int> Members { get; init; }

    /// <summary>直接子容器，先根序。</summary>
    public required IReadOnlyList<int> SubContainers { get; init; }
}
