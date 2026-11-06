using ErrorOr;

namespace Kuroe.Tool.FileAccess;

/// <summary>一次文本替换的条件：查找条件、归一换行后的 pattern 与 replacement。
/// 区分大小写时 pattern 与 replacement 相同拒绝，pattern 为空时拒绝。</summary>
public sealed record TextReplacement
{
    private TextReplacement(TextQuery query, string pattern, string replacement)
    {
        Query = query;
        Pattern = pattern;
        Replacement = replacement;
    }

    /// <summary>查找条件，提供后缀过滤、大小写规则与比较方式。</summary>
    public TextQuery Query { get; }

    /// <summary>归一换行后的查找文本。</summary>
    public string Pattern { get; }

    /// <summary>归一换行后的替换文本，可为空。</summary>
    public string Replacement { get; }

    /// <summary>字面匹配使用的比较方式。</summary>
    public StringComparison Comparison => Query.Comparison;

    /// <summary>建立替换条件，归一 pattern 与 replacement 的换行。缺 replacement、空 pattern 或区分大小写时两者相同拒绝。</summary>
    public static ErrorOr<TextReplacement> Create(TextQuery query, string? replacement)
    {
        if (replacement is null)
        {
            return ToolErrors.Argument("缺少参数 replacement。");
        }

        string pattern = TextNewLines.Normalize(query.Pattern);
        string text = TextNewLines.Normalize(replacement);
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return ToolErrors.Argument("参数 pattern 不能为空或空白。");
        }

        if (!query.IgnoreCase && pattern == text)
        {
            return ToolErrors.Argument("pattern 与 replacement 相同，替换不会改变文件。");
        }

        return new TextReplacement(query, pattern, text);
    }
}