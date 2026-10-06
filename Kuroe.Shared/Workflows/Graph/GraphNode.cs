using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Graph;

/// <summary>流程图上的节点实体：执行节点或容器。统一编号、路径与门控。
/// 执行节点会启动 run 并交回产出，容器是成员的组织与汇合点。</summary>
public abstract record GraphNode
{
    /// <summary>统一编号，先根序。</summary>
    public required int Index { get; init; }

    /// <summary>节点名。</summary>
    public required NodeName Name { get; init; }

    /// <summary>从根滑下来的路径，用于展示与定位。</summary>
    public required string Path { get; init; }

    /// <summary>产出后是否停在等待批准。</summary>
    public required NodeGate Gate { get; init; }
}
