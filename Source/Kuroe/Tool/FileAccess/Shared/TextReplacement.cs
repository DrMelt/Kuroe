using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>一次文本替换的条件：查找条件、归一换行后的模式与 replacement。
/// 字面模式下模式与替换文本相同拒绝，replacement 缺失拒绝。</summary>
public sealed record TextReplacement
{
    private TextReplacement(TextQuery query, ITextPattern pattern, string replacement)
    {
        Query = query;
        Pattern = pattern;
        Replacement = replacement;
    }

    /// <summary>查找条件，提供后缀过滤与模式种类。</summary>
    public TextQuery Query { get; }

    /// <summary>归一换行后的查找模式。</summary>
    public ITextPattern Pattern { get; }

    /// <summary>归一换行后的替换模板，可为空。</summary>
    public string Replacement { get; }

    /// <summary>建立替换条件，归一 pattern 与 replacement 的换行。
    /// 缺 replacement 或字面模式下两者相同拒绝。</summary>
    /// <param name="query">查找条件。</param>
    /// <param name="replacement">替换模板，可为空。</param>
    public static ErrorOr<TextReplacement> Create(TextQuery query, string? replacement)
    {
        if (replacement is null)
        {
            return ToolErrors.Argument("缺少参数 replacement。");
        }

        string pattern = NewLineStyle.Normalize(query.Pattern);
        string text = NewLineStyle.Normalize(replacement);
        if (!query.IsRegex && pattern == text)
        {
            return ToolErrors.Argument("pattern 与 replacement 相同，替换不会改变文件。");
        }

        ErrorOr<ITextPattern> normalized = query.CreatePattern(pattern);
        if (normalized.IsError)
        {
            return normalized.ErrorsOrEmptyList;
        }

        return new TextReplacement(query, normalized.Value, text);
    }
}