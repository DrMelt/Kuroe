namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>字面文本模式：按序数比较查找固定文本，替换模板原样写入。</summary>
public sealed class LiteralTextPattern : ITextPattern
{
    private LiteralTextPattern(string source)
    {
        Source = source;
    }

    /// <summary>模式原文。</summary>
    public string Source { get; }

    /// <summary>按序数比较建立字面模式。</summary>
    /// <param name="source">要查找的字面文本。</param>
    public static LiteralTextPattern Create(string source) => new(source);

    /// <summary>统计文本中的不重叠匹配，空文本不产生匹配。</summary>
    /// <param name="text">被查找的文本。</param>
    /// <param name="limit">统计的出现次数上限。</param>
    public TextMatchSummary Scan(string text, int limit)
    {
        if (Source.Length == 0)
        {
            return new TextMatchSummary(-1, 0);
        }

        int first = -1;
        int count = 0;
        int start = 0;
        while (count < limit && start <= text.Length - Source.Length)
        {
            int index = text.IndexOf(Source, start, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            if (first < 0)
            {
                first = index;
            }

            count++;
            start = index + Source.Length;
        }

        return new TextMatchSummary(first, count);
    }

    /// <summary>按出现顺序枚举替换，模板原样带出。</summary>
    /// <param name="text">被替换的文本。</param>
    /// <param name="template">替换模板。</param>
    public IEnumerable<TextMatchReplacement> Replace(string text, string template)
    {
        if (Source.Length == 0)
        {
            yield break;
        }

        int start = 0;
        while (start <= text.Length - Source.Length)
        {
            int index = text.IndexOf(Source, start, StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            yield return new TextMatchReplacement(index, Source.Length, template);
            start = index + Source.Length;
        }
    }
}