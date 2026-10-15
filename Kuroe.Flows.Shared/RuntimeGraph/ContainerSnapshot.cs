using Kuroe.Flows.Shared.StaticGraph;

namespace Kuroe.Flows.Shared.RuntimeGraph;

/// <summary>一个容器节点在某时刻的执行状态：内容齐备情况与成员活动。</summary>
public sealed record ContainerSnapshot(
    int Index,
    string Name,
    NodePath Path,
    NodeState State,
    IReadOnlyList<int> Members);
