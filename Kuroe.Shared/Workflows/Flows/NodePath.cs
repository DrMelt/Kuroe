namespace Kuroe.Shared.Workflows.Flows;

/// <summary>节点在流程树里的路径：从根到本节点的段序列，段按树层级排列，供定位与展示。</summary>
public readonly record struct NodePath(IReadOnlyList<NodeName> Levels)
{
    /// <summary>/ 分隔的路径文本，段是层级名。</summary>
    public string Value => string.Join('/', Levels.Select(level => level.Value));

    /// <summary>路径相等：段序列逐段相同即相等，不比较承载段的集合实例。</summary>
    public bool Equals(NodePath other) => Levels.SequenceEqual(other.Levels);

    /// <summary>路径哈希，按段序列组合。</summary>
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (NodeName level in Levels)
        {
            hash.Add(level);
        }
        return hash.ToHashCode();
    }

    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;
}
