using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Shared.Workflows.Flows;

/// <summary>流程编译出的执行视图：叶子按先根序展平，段记录拆分的消费范围。
/// 容器节点在此向前只提供作用域，执行一律按叶子序号进行。</summary>
public sealed record NodeGraph(IReadOnlyList<LeafNode> Leaves, IReadOnlyList<UnitSegment> Segments)
{
    private readonly Dictionary<string, int> _byName = Leaves
        .Select((leaf, index) => (leaf.Name, index))
        .ToDictionary(entry => entry.Name, entry => entry.index);

    private readonly int[] _segmentOfLeaf = BuildSegmentMap(Leaves.Count, Segments);

    /// <summary>叶子数。</summary>
    public int Count => Leaves.Count;

    /// <summary>取一片叶子。</summary>
    public LeafNode this[int index] => Leaves[index];

    /// <summary>按名定位叶子，不存在时为空。</summary>
    public int? IndexOf(string name) =>
        _byName.TryGetValue(name, out int index) ? index : null;

    /// <summary>叶子所属段，段外叶子为空。</summary>
    public UnitSegment? SegmentFor(int leafIndex) =>
        _segmentOfLeaf[leafIndex] is var segment && segment >= 0 ? Segments[segment] : null;

    /// <summary>消费该拆分产出的段，没有被消费时为空。</summary>
    public UnitSegment? SegmentConsuming(int planLeafIndex) =>
        Segments.FirstOrDefault(segment => segment.Source == planLeafIndex);

    /// <summary>并行段里以分支名定位分支叶子，分支不存在或非并行段时空。</summary>
    public int? BranchLeaf(UnitSegment segment, string? branch)
    {
        if (!segment.IsParallel || string.IsNullOrWhiteSpace(branch))
        {
            return null;
        }

        foreach (int index in segment.BranchLeaves)
        {
            if (Leaves[index].Name == branch)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>叶子 From 引用的拆分源叶子，没有引用规划产出时空。</summary>
    public int? PlanSource(int leafIndex)
    {
        foreach (int from in Leaves[leafIndex].From)
        {
            if (Leaves[from].Output == NodeOutput.Plan)
            {
                return from;
            }
        }

        return null;
    }

    /// <summary>收拢检查对齐的拆分来源：先看 From 里的规划产出，再从 From 引用的实施叶所属段推导。</summary>
    public int? FunnelSource(int checkIndex)
    {
        if (PlanSource(checkIndex) is { } plan)
        {
            return plan;
        }

        foreach (int from in Leaves[checkIndex].From)
        {
            if (SegmentFor(from) is { } segment)
            {
                return segment.Source;
            }
        }

        return null;
    }

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

    /// <summary>返工退回目标：分支单元退回它归属的分支叶，其余退回被检查的实施叶。</summary>
    public int? ReturnTarget(int checkIndex, string? branch)
    {
        if (branch is { } name)
        {
            foreach (int from in Leaves[checkIndex].From)
            {
                if (SegmentFor(from) is { IsParallel: true } segment && BranchLeaf(segment, name) is { } owned)
                {
                    return owned;
                }
            }
        }

        return FunnelReturn(checkIndex) ?? ImplementBefore(checkIndex);
    }

    /// <summary>检查叶返工需要连边的全部退回叶，去重保持顺序。</summary>
    public IReadOnlyList<int> ReturnTargets(int checkIndex)
    {
        HashSet<int> targets = [];
        foreach (int from in Leaves[checkIndex].From)
        {
            if (SegmentFor(from) is { IsParallel: true } segment)
            {
                targets.UnionWith(segment.BranchLeaves);
            }
        }

        if (FunnelReturn(checkIndex) is { } single)
        {
            targets.Add(single);
        }

        return [.. targets];
    }

    /// <summary>把各段的叶子范围落到逐叶归属，段外叶子记为 -1。</summary>
    private static int[] BuildSegmentMap(int leafCount, IReadOnlyList<UnitSegment> segments)
    {
        int[] map = new int[leafCount];
        Array.Fill(map, -1);
        for (int segment = 0; segment < segments.Count; segment++)
        {
            for (int index = segments[segment].Start; index < segments[segment].Exit; index++)
            {
                map[index] = segment;
            }
        }

        return map;
    }
}