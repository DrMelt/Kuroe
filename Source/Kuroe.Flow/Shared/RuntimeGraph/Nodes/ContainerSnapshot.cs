using Kuroe.Flow.Shared.Model;

namespace Kuroe.Flow.Shared.RuntimeGraph.Nodes;

/// <summary>一个容器节点在某时刻的执行状态：内容齐备情况与成员活动。</summary>
public sealed record ContainerSnapshot(
    NodePath Path,
    NodeState State,
    IReadOnlyList<NodePath> Children);
