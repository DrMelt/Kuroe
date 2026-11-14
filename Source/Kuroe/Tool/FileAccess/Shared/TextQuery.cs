using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本查找条件：模式文本、模式种类与后缀过滤。后缀在创建时归一为带前导点的小写形式。</summary>
public sealed record TextQuery
{
    private TextQuery(string pattern, IReadOnlyList<string> extensions, bool regex)
    {
        Pattern = pattern;
        Extensions = extensions;
        IsRegex = regex;
    }

    /// <summary>模式文本，正则模式下按原文解释。</summary>
    public string Pattern { get; }

    /// <summary>归一后的后缀名，带前导点，空集合表示不限制。</summary>
    public IReadOnlyList<string> Extensions { get; }

    /// <summary>pattern 是否按正则解释。</summary>
    public bool IsRegex { get; }

    /// <summary>从工具实参建立查找条件。空文本、空后缀条目或含通配符与路径分隔符的后缀条目拒绝。</summary>
    /// <param name="pattern">要查找的文本。</param>
    /// <param name="extensions">限定后缀名，空集合表示不限制。</param>
    /// <param name="regex">pattern 是否按正则解释。</param>
    public static ErrorOr<TextQuery> Create(
        string? pattern,
        IReadOnlyList<string>? extensions,
        bool regex = false)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return ToolErrors.Argument("缺少参数 pattern 或 pattern 为空。");
        }

        ErrorOr<IReadOnlyList<string>> normalized = Normalize(extensions);
        if (normalized.IsError)
        {
            return normalized.ErrorsOrEmptyList;
        }

        return new TextQuery(pattern, normalized.Value, regex);
    }

    /// <summary>按条件中的模式文本建立查找模式。</summary>
    public ErrorOr<ITextPattern> CreatePattern() => TextPatternFactory.Create(IsRegex, Pattern);

    /// <summary>按条件中的模式种类为另一份模式文本建立查找模式。</summary>
    /// <param name="source">模式文本。</param>
    public ErrorOr<ITextPattern> CreatePattern(string source) =>
        TextPatternFactory.Create(IsRegex, source);

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