namespace Kuroe.Tool;

/// <summary>工具路径：函数在工具层级里的位置，按 / 分段。白名单条目按路径前缀放行，写上级路径即包含整棵子树；
/// 工具定义的路径是要被放行的一侧，由函数名与分组拼出。条目只按文本比较。</summary>
public readonly record struct ToolPath(string Value)
{
    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;

    /// <summary>按函数名与可选分组拼出完整路径：有分组时是 分组/函数名，空分组与未给分组同等。</summary>
    public static ToolPath Of(ToolName name, ToolPath? group = null) =>
        group is { Value.Length: > 0 } parent ? new ToolPath($"{parent.Value}/{name.Value}") : new ToolPath(name.Value);

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