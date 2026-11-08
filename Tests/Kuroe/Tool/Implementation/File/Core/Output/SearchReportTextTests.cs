using ErrorOr;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;
using Xunit;

namespace Kuroe.Tool.File.Tests.Output;

/// <summary>搜索报告文本：无文件、无匹配与匹配列表。</summary>
public sealed class SearchReportTextTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kuroe-tests", "root");

    [Fact]
    public void ToString_reports_no_searchable_files()
    {
        TextSearchResult result = new([], ScannedFiles: 0, Occurrences: 0);

        string text = new SearchReportText(result, Query("needle"), "路径基准根", Root).ToString();

        Assert.Equal("在 路径基准根 中没有可搜索的文件。", text);
    }

    [Fact]
    public void ToString_reports_no_matching_text()
    {
        TextSearchResult result = new([], ScannedFiles: 3, Occurrences: 0);

        string text = new SearchReportText(result, Query("needle"), "路径基准根", Root).ToString();

        Assert.Equal("在 路径基准根 中搜索 needle 没有匹配，已扫描 3 个文件。", text);
    }

    [Fact]
    public void ToString_renders_matches_under_a_count_header()
    {
        TextSearchMatch[] matches = [new(Path.Combine(Root, "a.txt"), 2, "needle", 6, 0)];
        TextSearchResult result = new(matches, ScannedFiles: 1, Occurrences: 1);

        string text = new SearchReportText(result, Query("needle"), "路径基准根", Root).ToString();

        Assert.Equal(
            string.Join('\n', "在 路径基准根 中匹配 1 处：", "a.txt", "2 |needle"),
            text);
    }

    [Fact]
    public void ToString_marks_truncation()
    {
        string longText = new('a', TextOutputLimit.Characters + 1000);
        TextSearchMatch[] matches = [new(Path.Combine(Root, "a.txt"), 1, longText, longText.Length, 0)];
        TextSearchResult result = new(matches, ScannedFiles: 1, Occurrences: 1);

        string text = new SearchReportText(result, Query("a"), "路径基准根", Root).ToString();

        Assert.StartsWith($"（内容超过 {TextOutputLimit.Characters} 字符，已截断）\n", text, StringComparison.Ordinal);
    }

    private static TextQuery Query(string pattern)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, null, ignoreCase: false);
        Assert.False(query.IsError);

        return query.Value;
    }
}