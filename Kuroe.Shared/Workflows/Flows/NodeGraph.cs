namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程编译出的执行视图：叶子按先根序展平，供引擎推进与快照使用。
/// 容器节点在此向前只提供作用域，执行一律按叶子序号进行。</summary>
public sealed record NodeGraph(IReadOnlyList<LeafNode> Leaves)
{
    private readonly Dictionary<string, int> _byName = Leaves
        .Select((leaf, index) => (leaf.Name, index))
        .ToDictionary(entry => entry.Name, entry => entry.index);

    /// <summary>叶子数。</summary>
    public int Count => Leaves.Count;

    /// <summary>取一片叶子。</summary>
    public LeafNode this[int index] => Leaves[index];

    /// <summary>按名定位叶子，不存在时为空。</summary>
    public int? IndexOf(string name) =>
        _byName.TryGetValue(name, out int index) ? index : null;

    /// <summary>收拢叶：整叶汇拢展开的实施产出，由引擎按聚合路径执行。</summary>
    public bool IsFunnel(int leafIndex) =>
        Leaves[leafIndex] is { Output: NodeOutput.Review, Mode: NodeMode.Single }
        && FunnelReturn(leafIndex) is not null;

    /// <summary>收拢检查不通过时退回的展开实施叶，非收拢叶为空。</summary>
    public int? FunnelReturn(int leafIndex)
    {
        foreach (int from in Leaves[leafIndex].From)
        {
            if (Leaves[from].Mode == NodeMode.PerItem)
            {
                return from;
            }
        }

        return null;
    }

    /// <summary>该叶之前最近的实施叶，做一般返工退回用。</summary>
    public int? ImplementBefore(int leafIndex)
    {
        for (int index = leafIndex - 1; index >= 0; index--)
        {
            if (Leaves[index].Output == NodeOutput.Plain)
            {
                return index;
            }
        }

        return null;
    }
}