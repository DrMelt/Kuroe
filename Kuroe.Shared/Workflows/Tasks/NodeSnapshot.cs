using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows.Flows;

namespace Kuroe.Shared.Workflows.Tasks;

/// <summary>一片叶子在某一刻的只读形状，含该叶子上全部 agent。</summary>
public sealed record NodeSnapshot(int Index, LeafNode Leaf, IReadOnlyList<RunSnapshot> Runs);