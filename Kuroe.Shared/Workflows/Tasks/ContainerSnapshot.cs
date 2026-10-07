using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>一个容器节点在某时刻的执行状态：成员产出齐备情况与门控停留。</summary>
public sealed record ContainerSnapshot(
    int Index,
    string Name,
    NodePath Path,
    NodeState State,
    IReadOnlyList<int> Members);
