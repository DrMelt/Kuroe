using Kuroe.Shared.Workflows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>一个容器节点在某时刻的执行状态：成员产出齐备情况与门控停留。</summary>
public sealed record GroupSnapshot(
    int Index,
    string Name,
    string Path,
    NodeState State,
    IReadOnlyList<int> Members);