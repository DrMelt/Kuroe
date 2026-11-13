namespace Kuroe.Flow.Tasks.Shared;

/// <summary>流程步骤路径：从根到步骤的标签序列。流程控制与宿主只使用它，不依赖具体流程实现。</summary>
public readonly record struct FlowStepPath(IReadOnlyList<string> Levels)
{
    /// <summary>按标签从根依次构造路径。</summary>
    public static FlowStepPath Of(params string[] levels) => new([.. levels]);

    /// <summary>/ 分隔的路径文本。</summary>
    public string Value => string.Join('/', Levels);

    /// <summary>路径相等：标签序列逐段相同。</summary>
    public bool Equals(FlowStepPath other) => Levels.SequenceEqual(other.Levels);

    /// <summary>路径哈希，按标签序列组合。</summary>
    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (string level in Levels)
        {
            hash.Add(level);
        }

        return hash.ToHashCode();
    }

    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;
}