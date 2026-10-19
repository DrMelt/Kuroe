namespace Kuroe.Tool;

/// <summary>工具路径：函数在工具层级里的位置，按 / 分段，同时是模型调用名。白名单条目按路径前缀放行，写上级路径即包含整棵子树；
/// 条目只按文本比较。末段是函数名。</summary>
public readonly record struct ToolPath(string Value)
{
    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;

    /// <summary>路径末段的函数名，供宿主展示。</summary>
    public ToolName Name => new(Value[(Value.LastIndexOf('/') + 1)..]);

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