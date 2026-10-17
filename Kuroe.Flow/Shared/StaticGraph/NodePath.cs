namespace Kuroe.Flow.Shared.StaticGraph;

/// <summary>节点在流程树里的路径：从根到本节点的段序列，段是节点名。路径是节点的标识，唯一性来自树位置。</summary>
public readonly record struct NodePath(IReadOnlyList<NodeName> Levels)
{
    /// <summary>按段名从根依次构造路径。</summary>
    public static NodePath Of(params string[] levels) =>
        new([.. levels.Select(level => new NodeName(level))]);

    /// <summary>本节点的名字，路径的最后一段。</summary>
    public NodeName Name => Levels[^1];

    /// <summary>所属容器的路径，根级节点为空。</summary>
    public NodePath? Parent => Levels.Count > 1 ? new NodePath([.. Levels.Take(Levels.Count - 1)]) : null;

    /// <summary>段数。</summary>
    public int Depth => Levels.Count;

    /// <summary>在末尾追加一段。</summary>
    public NodePath Append(NodeName name) => new([.. Levels, name]);

    /// <summary>本路径是否落在另一条路径之下，同一路径不算。</summary>
    public bool IsUnder(NodePath other) =>
        Depth > other.Depth && Levels.Take(other.Depth).SequenceEqual(other.Levels);

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
