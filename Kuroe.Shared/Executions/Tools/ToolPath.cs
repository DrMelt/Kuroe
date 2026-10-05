namespace Kuroe.Shared.Executions.Tools;

/// <summary>工具路径：函数在层级里的位置，按 / 分段，白名单按路径前缀放行，写上级路径即包含整棵子树。</summary>
public readonly record struct ToolPath(string Value)
{
    /// <summary>规划的契约路径：规划执行节点按产出契约附上的工具。</summary>
    public static ToolPath ContractPlan { get; } = new(ToolName.ContractPlan.Value);

    /// <summary>路径的文本形式。</summary>
    public override string ToString() => Value;

    /// <summary>本路径是否在给定路径之下或相等。空路径不匹配任何工具。</summary>
    public bool IsUnder(ToolPath ancestor) =>
        ancestor.Value.Length > 0
        && Value.Length >= ancestor.Value.Length
        && Value.StartsWith(ancestor.Value, StringComparison.Ordinal)
        && (Value.Length == ancestor.Value.Length || Value[ancestor.Value.Length] == '/');

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
