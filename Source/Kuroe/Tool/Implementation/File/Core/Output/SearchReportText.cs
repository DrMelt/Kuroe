using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File.Output;

/// <summary>搜索报告文本：匹配计数、按文件分组的匹配行与输出上限。</summary>
/// <param name="Result">搜索结果。</param>
/// <param name="Query">查找条件。</param>
/// <param name="Scope">搜索范围的展示文本。</param>
/// <param name="Root">相对路径的基准目录。</param>
public readonly record struct SearchReportText(
    TextSearchResult Result,
    TextQuery Query,
    string Scope,
    string Root)
{
    /// <summary>搜索报告文本，整体超过输出上限时截断并提示。</summary>
    public override string ToString()
    {
        if (Result.Matches.Count == 0)
        {
            return Result.ScannedFiles == 0
                ? NoSearchableFiles(Scope)
                : NoMatchingText(Result, Query, Scope);
        }

        string header = $"在 {Scope} 中匹配 {Result.Occurrences} 处：";
        return new LimitedText(
            new SearchMatchListText(Result.Matches, header, Root).ToString(),
            TextOutputLimit.Characters).ToString();
    }

    /// <summary>范围内没有可搜索文件时的文本。</summary>
    public static string NoSearchableFiles(string scope) => $"在 {scope} 中没有可搜索的文件。";

    /// <summary>搜索完成但没有匹配时的文本，报告扫描的文件数。</summary>
    private static string NoMatchingText(TextSearchResult result, TextQuery query, string scope) =>
        $"在 {scope} 中搜索 {query.Pattern} 没有匹配，已扫描 {result.ScannedFiles} 个文件。";
}