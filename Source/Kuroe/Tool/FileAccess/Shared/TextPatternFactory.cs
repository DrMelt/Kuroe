using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>按模式种类建立文本查找模式。</summary>
internal static class TextPatternFactory
{
    /// <summary>按是否正则建立模式，正则模式非法时返回参数错误。</summary>
    /// <param name="regex">是否按正则解释。</param>
    /// <param name="source">模式文本。</param>
    internal static ErrorOr<ITextPattern> Create(bool regex, string source)
    {
        if (!regex)
        {
            return LiteralTextPattern.Create(source);
        }

        ErrorOr<RegexTextPattern> pattern = RegexTextPattern.Create(source);
        if (pattern.IsError)
        {
            return pattern.ErrorsOrEmptyList;
        }

        return pattern.Value;
    }
}