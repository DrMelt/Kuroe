using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>文本搜索条件：字面文本、后缀过滤与大小写规则。后缀在创建时归一为带前导点的小写形式。</summary>
public sealed record TextSearchQuery
{
    private TextSearchQuery(string pattern, IReadOnlyList<string> extensions, bool ignoreCase)
    {
        Pattern = pattern;
        Extensions = extensions;
        IgnoreCase = ignoreCase;
    }

    /// <summary>要匹配的字面文本。</summary>
    public string Pattern { get; }

    /// <summary>归一后的后缀名，带前导点，空集合表示不限制。</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>匹配是否忽略大小写。</summary>
    public bool IgnoreCase { get; }

    /// <summary>从工具实参建立查询条件。空文本、空后缀条目或含通配符与路径分隔符的后缀条目拒绝。</summary>
    public static ErrorOr<TextSearchQuery> Create(
        string? pattern,
        IReadOnlyList<string>? extensions,
        bool ignoreCase)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return ToolErrors.Argument("缺少参数 pattern 或 pattern 为空。");
        }

        ErrorOr<IReadOnlyList<string>> normalized = Normalize(extensions);
        if (normalized.IsError)
        {
            return normalized.ErrorsOrEmptyList;
        }

        return new TextSearchQuery(pattern, normalized.Value, ignoreCase);
    }

    /// <summary>该行是否包含搜索文本。</summary>
    public bool Matches(string line) =>
        line.Contains(Pattern, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>文件名是否符合后缀过滤，未给后缀时全部符合。</summary>
    public bool MatchesExtension(string file) =>
        Extensions.Count == 0
        || Extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);

    /// <summary>归一后缀条目：去空白，拒绝空项、通配符与路径分隔符，补前导点并去重。</summary>
    private static ErrorOr<IReadOnlyList<string>> Normalize(IReadOnlyList<string>? extensions)
    {
        if (extensions is null || extensions.Count == 0)
        {
            return Array.Empty<string>();
        }

        List<string> normalized = [];
        foreach (string extension in extensions)
        {
            string value = extension.Trim();
            if (value.Length == 0)
            {
                return ToolErrors.Argument("后缀名 extensions 不能包含空条目。");
            }

            if (value.Contains('*') || value.Contains('?') || value.Contains('/') || value.Contains('\\'))
            {
                return ToolErrors.Argument($"后缀名 {extension} 只写扩展名，不写通配符或路径。");
            }

            normalized.Add((value.StartsWith('.') ? value : $".{value}").ToLowerInvariant());
        }

        return normalized.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}