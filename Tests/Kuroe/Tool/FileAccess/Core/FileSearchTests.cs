using System.Text;
using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

using File = System.IO.File;

/// <summary>根目录内的文本搜索：范围展开、后缀过滤、字面匹配与输出字符上限。</summary>
public sealed class FileSearchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    public FileSearchTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Search_returns_matches_in_path_order()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "b.txt"), "needle\n");
        File.WriteAllText(Path.Combine(_root, "a.txt"), "first\nneedle\n");
        File.WriteAllText(Path.Combine(_root, "sub", "c.txt"), "needle\n");

        TextSearchResult result = Run([], Query("needle"));

        Assert.Equal(
            ["a.txt", "b.txt", Path.Combine("sub", "c.txt")],
            result.Matches.Select(match => Path.GetRelativePath(_root, match.File)));
        Assert.Equal([2, 1, 1], result.Matches.Select(match => match.Line));
        Assert.Equal(3, result.ScannedFiles);
    }

    [Fact]
    public void Search_filters_by_scope_and_extension()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Source"));
        Directory.CreateDirectory(Path.Combine(_root, "Tests"));
        File.WriteAllText(Path.Combine(_root, "Source", "a.cs"), "needle\n");
        File.WriteAllText(Path.Combine(_root, "Source", "b.md"), "needle\n");
        File.WriteAllText(Path.Combine(_root, "Tests", "c.cs"), "needle\n");

        TextSearchResult result = Run(["Source"], Query("needle", ["cs"]));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.EndsWith(Path.Combine("Source", "a.cs"), match.File, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_restricts_to_explicit_files_and_deduplicates_overlaps()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "needle\n");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "needle\n");

        TextSearchResult result = Run(["a.txt", "."], Query("needle"));

        Assert.Equal(2, result.Matches.Count);
        Assert.Single(result.Matches, match => Path.GetFileName(match.File) == "a.txt");
    }

    [Fact]
    public void Search_filters_explicit_files_by_extension()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "needle\n");

        TextSearchResult result = Run(["a.txt"], Query("needle", ["md"]));

        Assert.Empty(result.Matches);
        Assert.Equal(0, result.ScannedFiles);
    }

    [Fact]
    public void Search_skips_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "needle\n");
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "needle\n");
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "needle\n");

        TextSearchResult result = Run([], Query("needle"), new ToolPathAccess([], [".kuroe", ".git"]));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("visible.txt", Path.GetFileName(match.File));
    }

    [Fact]
    public void Search_skips_binary_files()
    {
        File.WriteAllText(Path.Combine(_root, "text.txt"), "needle\n");
        File.WriteAllBytes(Path.Combine(_root, "binary.bin"), [0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0x00, 0x0A]);

        TextSearchResult result = Run([], Query("needle"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("text.txt", Path.GetFileName(match.File));
        Assert.Equal(2, result.ScannedFiles);
        Assert.Equal(1, result.SkippedFiles);
    }

    [Fact]
    public void Search_reads_utf16_files_with_bom()
    {
        File.WriteAllText(Path.Combine(_root, "utf16.txt"), "needle", Encoding.Unicode);

        TextSearchResult result = Run([], Query("needle"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("needle", match.Text);
    }

    [Fact]
    public void Search_stops_at_the_character_budget()
    {
        File.WriteAllText(
            Path.Combine(_root, "long.txt"),
            string.Join('\n', Enumerable.Range(1, 100).Select(_ => $"needle{new string('a', 100)}")));

        TextSearchResult limited = Run([], Query("needle"), characterBudget: 200);
        TextSearchResult full = Run([], Query("needle"), characterBudget: int.MaxValue);

        Assert.NotEmpty(limited.Matches);
        Assert.True(limited.Matches.Count < full.Matches.Count);
        Assert.Equal(100, full.Matches.Count);
    }

    [Fact]
    public void Search_splits_lines_on_carriage_returns_and_line_feeds()
    {
        File.WriteAllText(Path.Combine(_root, "mixed.txt"), "a1\r\na2\ra3\na4");

        TextSearchResult result = Run([], Query("a"), characterBudget: int.MaxValue);

        Assert.Equal([1, 2, 3, 4], result.Matches.Select(match => match.Line));
        Assert.Equal(["a1", "a2", "a3", "a4"], result.Matches.Select(match => match.Text));
    }

    [Fact]
    public void Search_truncates_a_long_line_and_keeps_later_lines()
    {
        File.WriteAllText(
            Path.Combine(_root, "long.txt"),
            $"needle{new string('a', 9000)}\nneedle");

        IReadOnlyList<TextSearchMatch> matches = Run([], Query("needle"), characterBudget: int.MaxValue).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal(FileScope.OutputLimit + 1, matches[0].Text.Length);
        Assert.EndsWith("…", matches[0].Text, StringComparison.Ordinal);
        Assert.Equal(2, matches[1].Line);
        Assert.Equal("needle", matches[1].Text);
    }

    [Fact]
    public void Search_rejects_a_missing_scope()
    {
        ErrorOr<TextSearchResult> result = new FileSearch(Scope()).Search(["nope"], Query("needle"), FileScope.OutputLimit);

        Assert.True(result.IsError);
        Assert.Contains("不存在", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Query_normalizes_extensions_and_rejects_invalid_entries()
    {
        TextSearchQuery query = Query("x", ["CS", ".md", "cs"]);

        Assert.Equal([".cs", ".md"], query.Extensions);
        Assert.True(TextSearchQuery.Create(" ", null, false).IsError);
        Assert.True(TextSearchQuery.Create("x", [""], false).IsError);
        Assert.True(TextSearchQuery.Create("x", ["*.cs"], false).IsError);
        Assert.True(TextSearchQuery.Create("x", ["c?.cs"], false).IsError);
        Assert.True(TextSearchQuery.Create("x", ["Source/cs"], false).IsError);
    }

    [Fact]
    public void Search_is_case_sensitive_by_default()
    {
        File.WriteAllText(Path.Combine(_root, "a.txt"), "Needle\n");

        Assert.Empty(Run([], Query("needle")).Matches);
        Assert.Single(Run([], Query("needle", ignoreCase: true)).Matches);
    }

    private FileScope Scope() => new(_root, "工作目录");

    private TextSearchResult Run(
        IReadOnlyList<string> paths,
        TextSearchQuery query,
        ToolPathAccess? access = null,
        int characterBudget = FileScope.OutputLimit)
    {
        ErrorOr<TextSearchResult> result = new FileSearch(Scope(), access).Search(paths, query, characterBudget);
        Assert.False(result.IsError);
        return result.Value;
    }

    private static TextSearchQuery Query(
        string pattern,
        IReadOnlyList<string>? extensions = null,
        bool ignoreCase = false)
    {
        ErrorOr<TextSearchQuery> query = TextSearchQuery.Create(pattern, extensions, ignoreCase);
        Assert.False(query.IsError);
        return query.Value;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}