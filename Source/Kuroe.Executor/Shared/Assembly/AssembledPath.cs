namespace Kuroe.Executor.Shared.Assembly;

/// <summary>装配后节点在流程树里的路径：从根到本节点的段序列，段是节点标签。路径是节点的标识，唯一性来自树位置。</summary>
public readonly record struct AssembledPath(IReadOnlyList<string> Segments)
{
    /// <summary>按段标签从根依次构造路径。</summary>
    public static AssembledPath Of(params string[] segments) => new([.. segments]);

    /// <summary>本节点的标签，路径的最后一段。</summary>
    public string Label => Segments[^1];

    /// <summary>所属容器的路径，根级节点为空。</summary>
    public AssembledPath? Parent => Segments.Count > 1 ? new AssembledPath([.. Segments.Take(Segments.Count - 1)]) : null;

    /// <summary>段数。</summary>
    public int Depth => Segments.Count;

    /// <summary>在末尾追加一段。</summary>
    public AssembledPath Append(string segment) => new([.. Segments, segment]);

    /// <summary>本路径是否落在另一条路径之下，同一路径不算。</summary>
    public bool IsUnder(AssembledPath other) =>
        Depth > other.Depth && Segments.Take(other.Depth).SequenceEqual(other.Segments);

    /// <summary>/ 分隔的路径文本，段是层级标签。</summary>
    public string Value => string.Join('/', Segments);

    /// <summary>路径相等：段序列逐段相同即相等，不比较承载段的集合实例。</summary>
    public bool Equals(AssembledPath other) => Segments.SequenceEqual(other.Segments);

    /// <summary>路径哈希，按段序列组合。</summary>
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (string segment in Segments)
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }

    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;
}