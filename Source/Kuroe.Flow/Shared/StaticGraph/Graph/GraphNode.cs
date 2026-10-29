using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.StaticGraph.Graph;

/// <summary>流程图上的节点实体：开始、执行节点或容器。以从根到本节点的路径标识，路径唯一性来自树位置。
/// 开始是流程的唯一来源，执行节点会启动执行并交回产出，容器是成员的组织与汇合点。</summary>
public abstract record GraphNode
{
    /// <summary>从根到本节点的路径，流程内唯一，是节点的标识。</summary>
    public required NodePath Path { get; init; }

    /// <summary>节点标签，路径的最后一段。</summary>
    public NodeLabel Label => Path.Label;

    /// <summary>以本节点为来源的边，编译期落定，按编译顺序。</summary>
    public IReadOnlyList<FlowEdge> Outgoing { get; init; } = [];
}
