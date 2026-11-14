using System.Text.RegularExpressions;
using ErrorOr;

namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>正则文本模式：使用非回溯引擎查找，替换模板按分组引用展开。</summary>
public sealed class RegexTextPattern : ITextPattern
{
    private readonly Regex _regex;

    private RegexTextPattern(string source, Regex regex)
    {
        Source = source;
        _regex = regex;
    }

    /// <summary>模式原文。</summary>
    public string Source { get; }

    /// <summary>建立正则模式。非法语法与非回溯引擎不支持的构造返回参数错误。</summary>
    /// <param name="source">正则表达式。</param>
    public static ErrorOr<RegexTextPattern> Create(string source)
    {
        RegexOptions options = RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

        try
        {
            return new RegexTextPattern(source, new Regex(source, options));
        }
        catch (RegexParseException ex)
        {
            return ToolErrors.Argument($"pattern 不是合法的正则表达式，错误位置 {ex.Offset}。");
        }
        catch (NotSupportedException)
        {
            return ToolErrors.Argument(
                "pattern 使用了非回溯引擎不支持的构造，环视、反向引用、原子组、条件构造、\\G 与平衡组都不支持。");
        }
    }

    /// <summary>统计文本中的不重叠匹配，零宽匹配按单个字符推进。</summary>
    /// <param name="text">被查找的文本。</param>
    /// <param name="limit">统计的出现次数上限。</param>
    public TextMatchSummary Scan(string text, int limit)
    {
        if (limit < 1)
        {
            return new TextMatchSummary(-1, 0);
        }

        int first = -1;
        int count = 0;
        foreach (ValueMatch match in _regex.EnumerateMatches(text))
        {
            if (count >= limit)
            {
                break;
            }

            if (first < 0)
            {
                first = match.Index;
            }

            count++;
        }

        return new TextMatchSummary(first, count);
    }

    /// <summary>按出现顺序枚举替换，模板按分组引用展开，未知分组引用按字面写入。</summary>
    /// <param name="text">被替换的文本。</param>
    /// <param name="template">替换模板。</param>
    public IEnumerable<TextMatchReplacement> Replace(string text, string template)
    {
        foreach (Match match in _regex.Matches(text))
        {
            yield return new TextMatchReplacement(match.Index, match.Length, match.Result(template));
        }
    }
}