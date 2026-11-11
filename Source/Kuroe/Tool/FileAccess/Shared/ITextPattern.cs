namespace Kuroe.Tool.FileAccess.Shared;

/// <summary>文本查找模式：按模式语义统计匹配，并按模板展开替换文本。</summary>
public interface ITextPattern
{
    /// <summary>模式原文，用于报告。</summary>
    string Source { get; }

    /// <summary>统计文本中的不重叠匹配，最多 limit 处。没有匹配时起点为 -1。</summary>
    /// <param name="text">被查找的文本。</param>
    /// <param name="limit">统计的出现次数上限。</param>
    TextMatchSummary Scan(string text, int limit);

    /// <summary>按出现顺序枚举不重叠匹配与按模板展开的替换文本。</summary>
    /// <param name="text">被替换的文本。</param>
    /// <param name="template">替换模板。</param>
    IEnumerable<TextMatchReplacement> Replace(string text, string template);
}