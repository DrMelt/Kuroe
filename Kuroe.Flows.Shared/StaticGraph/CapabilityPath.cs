namespace Kuroe.Flows.Shared.StaticGraph;

/// <summary>能力路径：执行节点声明的能力条目，按 / 分段，白名单按路径前缀放行，写上级路径即包含整棵子树。
/// 条目只按文本比较，被比较路径的含义由宿主解释。</summary>
public readonly record struct CapabilityPath(string Value)
{
    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;

    /// <summary>本条目是否放行给定路径：路径落在本条目之下或与之相等。空条目不匹配任何路径。</summary>
    public bool Allows(string path) =>
        Value.Length > 0
        && path.Length >= Value.Length
        && path.StartsWith(Value, StringComparison.Ordinal)
        && (path.Length == Value.Length || path[Value.Length] == '/');

    /// <summary>路径格式的非法原因：首尾不能是 /，段非空且不含空白与花括号。未写或空时返回 null。</summary>
    public static string? InvalidReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value[0] == '/' || value[^1] == '/')
        {
            return $"路径 {value} 不能以 / 开头或结尾。";
        }

        string? illegal = value.Split('/').FirstOrDefault(segment =>
            segment.Length == 0 || segment.Any(char.IsWhiteSpace) || segment.Contains('{') || segment.Contains('}'));

        return illegal is { } segment ? $"路径 {value} 的段 {segment} 不能为空或含空白与花括号。" : null;
    }
}
