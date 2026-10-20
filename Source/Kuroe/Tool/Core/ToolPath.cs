namespace Kuroe.Tool;

/// <summary>工具路径：函数在工具层级里的位置，按 / 分段。白名单条目按路径前缀放行，写上级路径即包含整棵子树；
/// 条目只按文本比较。末段是函数名，模型调用名由路径派生。</summary>
public readonly record struct ToolPath(string Value)
{
    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;

    /// <summary>路径末段的函数名，供宿主展示。</summary>
    public ToolName Name => new(Value[(Value.LastIndexOf('/') + 1)..]);

    /// <summary>模型调用名：路径的扁平形式，层级分隔符换成下划线。模型接口只接受字母、数字、下划线与连字符，
    /// 路径段的字符集与之相同。两条路径的调用名可能相同，装配处按它查重。</summary>
    public ToolName CallName => new(Value.Replace('/', '_'));

    /// <summary>本条目是否放行给定路径：路径落在本条目之下或与之相等。空条目不匹配任何路径。</summary>
    public bool Allows(string path) =>
        Value.Length > 0
        && path.Length >= Value.Length
        && path.StartsWith(Value, StringComparison.Ordinal)
        && (path.Length == Value.Length || path[Value.Length] == '/');

    /// <summary>路径格式的非法原因：首尾不能是 /，段非空且只含模型调用名允许的字符。未写或空时返回 null。</summary>
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

        if (value.Split('/').FirstOrDefault(segment => !IsSegment(segment)) is not { } illegal)
        {
            return null;
        }

        return illegal.Length == 0
            ? $"路径 {value} 的段不能为空。"
            : $"路径 {value} 的段 {illegal} 只能写字母、数字、下划线与连字符。";
    }

    /// <summary>段是否合法：非空且只含模型调用名允许的字符。</summary>
    private static bool IsSegment(string segment) =>
        segment.Length > 0 && segment.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');
}