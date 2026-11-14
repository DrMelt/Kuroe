using System.Text;
using ErrorOr;
using Kuroe.TestSupport;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>根目录内的文本搜索：范围展开、后缀过滤、字面匹配与输出字符上限。</summary>
public sealed class FileSearchTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public void Search_returns_matches_in_path_order()
    {
        _files.AddFile(Path.Combine(_root, "b.txt"), "needle\n");
        _files.AddFile(Path.Combine(_root, "a.txt"), "first\nneedle\n");
        _files.AddFile(Path.Combine(_root, "sub", "c.txt"), "needle\n");

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
        _files.AddFile(Path.Combine(_root, "Source", "a.cs"), "needle\n");
        _files.AddFile(Path.Combine(_root, "Source", "b.md"), "needle\n");
        _files.AddFile(Path.Combine(_root, "Tests", "c.cs"), "needle\n");

        TextSearchResult result = Run(["Source"], Query("needle", ["cs"]));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.EndsWith(Path.Combine("Source", "a.cs"), match.File, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_restricts_to_explicit_files_and_deduplicates_overlaps()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "needle\n");
        _files.AddFile(Path.Combine(_root, "b.txt"), "needle\n");

        TextSearchResult result = Run(["a.txt", "."], Query("needle"));

        Assert.Equal(2, result.Matches.Count);
        Assert.Single(result.Matches, match => Path.GetFileName(match.File) == "a.txt");
    }

    [Fact]
    public void Search_filters_explicit_files_by_extension()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "needle\n");

        TextSearchResult result = Run(["a.txt"], Query("needle", ["md"]));

        Assert.Empty(result.Matches);
        Assert.Equal(0, result.ScannedFiles);
    }

    [Fact]
    public void Search_skips_denied_directories()
    {
        _files.AddFile(Path.Combine(_root, ".kuroe", "catalog.json"), "needle\n");
        _files.AddFile(Path.Combine(_root, ".git", "config"), "needle\n");
        _files.AddFile(Path.Combine(_root, "visible.txt"), "needle\n");

        TextSearchResult result = Run([], Query("needle"), new ToolPathAccess([], [".kuroe", ".git"]));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("visible.txt", Path.GetFileName(match.File));
    }

    [Fact]
    public void Search_skips_binary_files()
    {
        _files.AddFile(Path.Combine(_root, "text.txt"), "needle\n");
        _files.AddBytes(Path.Combine(_root, "binary.bin"), [0x6E, 0x65, 0x65, 0x64, 0x6C, 0x65, 0x00, 0x0A]);

        TextSearchResult result = Run([], Query("needle"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("text.txt", Path.GetFileName(match.File));
        Assert.Equal(2, result.ScannedFiles);
    }

    [Fact]
    public void Search_reads_utf16_files_with_bom()
    {
        _files.AddFile(Path.Combine(_root, "utf16.txt"), "needle", Encoding.Unicode);

        TextSearchResult result = Run([], Query("needle"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("needle", match.Text);
    }

    [Fact]
    public void Search_stops_at_the_character_budget()
    {
        _files.AddFile(
            Path.Combine(_root, "long.txt"),
            string.Join('\n', Enumerable.Range(1, 100).Select(_ => $"needle{new string('a', 100)}")));

        TextSearchResult limited = Run([], Query("needle"), characterBudget: 200);
        TextSearchResult full = Run([], Query("needle"), characterBudget: int.MaxValue);

        Assert.NotEmpty(limited.Matches);
        Assert.True(limited.Matches.Count < full.Matches.Count);
        Assert.Equal(100, full.Matches.Count);
    }

    [Fact]
    public void Search_counts_all_occurrences_on_a_line()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "needle needle\n");

        TextSearchResult result = Run([], Query("needle"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("needle needle", match.Text);
        Assert.Equal(2, result.Occurrences);
    }

    [Fact]
    public void Search_stops_at_the_occurrence_limit()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "needle\nneedle\nneedle\n");

        TextSearchResult result = Run(
            [],
            Query("needle"),
            characterBudget: int.MaxValue,
            occurrenceLimit: 2);

        Assert.Equal(2, result.Matches.Count);
        Assert.Equal(2, result.Occurrences);
    }

    [Fact]
    public void Search_counts_non_overlapping_occurrences()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "aaa\n");

        TextSearchResult result = Run([], Query("aa"));

        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("aaa", match.Text);
        Assert.Equal(1, result.Occurrences);
    }

    [Fact]
    public void Search_stops_at_the_character_budget_before_the_occurrence_limit()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "needle\nneedle\n");

        TextSearchResult result = Run([], Query("needle"), characterBudget: 1, occurrenceLimit: 2);

        Assert.Equal(1, result.Occurrences);
    }

    [Fact]
    public void Search_rejects_a_non_positive_occurrence_limit()
    {
        ErrorOr<TextSearchResult> result = new FileSearch(Scope())
            .Search([], Query("needle"), TextOutputLimit.Characters, 0);

        Assert.True(result.IsError);
        Assert.Contains("出现次数上限", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_splits_lines_on_carriage_returns_and_line_feeds()
    {
        _files.AddFile(Path.Combine(_root, "mixed.txt"), "a1\r\na2\ra3\na4");

        TextSearchResult result = Run([], Query("a"), characterBudget: int.MaxValue);

        Assert.Equal([1, 2, 3, 4], result.Matches.Select(match => match.Line));
        Assert.Equal(["a1", "a2", "a3", "a4"], result.Matches.Select(match => match.Text));
    }

    [Fact]
    public void Search_counts_crlf_as_two_terminator_characters()
    {
        _files.AddFile(Path.Combine(_root, "crlf.txt"), "needle\r\n");

        TextSearchMatch match = Assert.Single(Run([], Query("needle")).Matches);

        Assert.Equal(6, match.ContentLength);
        Assert.Equal(2, match.TerminatorLength);
        Assert.Equal(8, match.LineLength);
    }

    [Fact]
    public void Search_truncates_display_text_and_keeps_later_lines()
    {
        _files.AddFile(
            Path.Combine(_root, "long.txt"),
            $"needle{new string('a', 1495)}\nneedle");

        IReadOnlyList<TextSearchMatch> matches = Run([], Query("needle"), characterBudget: int.MaxValue).Matches;

        Assert.Equal(2, matches.Count);
        Assert.Equal($"needle{new string('a', 994)}", matches[0].Text);
        Assert.Equal(0, matches[0].Offset);
        Assert.Equal(1501, matches[0].ContentLength);
        Assert.Equal(1, matches[0].TerminatorLength);
        Assert.Equal(1502, matches[0].LineLength);
        Assert.True(matches[0].Truncated);
        Assert.Equal(2, matches[1].Line);
        Assert.Equal("needle", matches[1].Text);
        Assert.Equal(0, matches[1].Offset);
        Assert.False(matches[1].Truncated);
    }

    [Fact]
    public void Search_keeps_the_match_after_the_display_limit_visible()
    {
        _files.AddFile(
            Path.Combine(_root, "long.txt"),
            $"{new string('a', TextSearchMatch.DisplayLimit)}needle");

        TextSearchMatch match = Assert.Single(
            Run([], Query("needle"), characterBudget: int.MaxValue).Matches);

        Assert.Equal(TextSearchMatch.DisplayLimit, match.Text.Length);
        Assert.Equal(TextSearchMatch.DisplayLimit + 6, match.LineLength);
        Assert.Equal(6, match.Offset);
        Assert.Contains("needle", match.Text, StringComparison.Ordinal);
        Assert.True(match.Truncated);
    }

    [Fact]
    public void Search_centers_the_display_window_on_the_match()
    {
        string line = $"{new string('a', 700)}needle{new string('b', 794)}";
        _files.AddFile(Path.Combine(_root, "long.txt"), line);

        TextSearchMatch match = Assert.Single(
            Run([], Query("needle"), characterBudget: int.MaxValue).Matches);

        Assert.Equal(200, match.Offset);
        Assert.Equal(line[200..], match.Text + new string('b', 300));
        Assert.Equal(TextSearchMatch.DisplayLimit, match.Text.Length);
        Assert.Contains("needle", match.Text, StringComparison.Ordinal);
        Assert.True(match.Truncated);
    }

    [Fact]
    public void Search_does_not_split_surrogate_pairs_at_the_window_edges()
    {
        string emoji = char.ConvertFromUtf32(0x1F600);
        string leading = $"{new string('a', 499)}{emoji}{new string('a', 499)}needle{new string('b', 493)}";
        string trailing = $"{new string('a', 1000)}needle{new string('a', 493)}{emoji}{new string('b', 100)}";
        _files.AddFile(Path.Combine(_root, "a.txt"), leading);
        _files.AddFile(Path.Combine(_root, "b.txt"), trailing);

        IReadOnlyList<TextSearchMatch> matches = Run([], Query("needle"), characterBudget: int.MaxValue).Matches;

        Assert.Equal(2, matches.Count);
        TextSearchMatch first = Assert.Single(matches, match => Path.GetFileName(match.File) == "a.txt");
        TextSearchMatch second = Assert.Single(matches, match => Path.GetFileName(match.File) == "b.txt");
        Assert.Equal(499, first.Offset);
        Assert.True(char.IsHighSurrogate(first.Text[0]));
        Assert.True(char.IsLowSurrogate(first.Text[1]));
        Assert.Equal(999, second.Text.Length);
        Assert.False(char.IsLowSurrogate(second.Text[^1]));
    }

    [Fact]
    public void Search_keeps_a_line_at_the_display_limit_untruncated()
    {
        string line = $"needle{new string('a', TextSearchMatch.DisplayLimit - 6)}";
        _files.AddFile(Path.Combine(_root, "exact.txt"), line);

        TextSearchMatch match = Assert.Single(
            Run([], Query("needle"), characterBudget: int.MaxValue).Matches);

        Assert.Equal(line, match.Text);
        Assert.Equal(0, match.Offset);
        Assert.Equal(TextSearchMatch.DisplayLimit, match.LineLength);
        Assert.False(match.Truncated);
    }

    [Fact]
    public void Search_rejects_a_missing_scope()
    {
        ErrorOr<TextSearchResult> result = new FileSearch(Scope())
            .Search(["nope"], Query("needle"), TextOutputLimit.Characters);

        Assert.True(result.IsError);
        Assert.Contains("不存在", result.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_is_case_sensitive()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "Needle\n");

        Assert.Empty(Run([], Query("needle")).Matches);
    }

    [Fact]
    public void Search_accepts_a_whitespace_pattern()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "a b\n");

        TextSearchResult result = Run([], Query(" "));

        Assert.Equal(1, result.Occurrences);
        Assert.Equal("a b", Assert.Single(result.Matches).Text);
    }

    [Fact]
    public void Search_treats_anchors_as_line_boundaries()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "L1\nL22\nL333\n");

        TextSearchResult result = Run([], Query(@"^L\d+$", regex: true));

        Assert.Equal([1, 2, 3], result.Matches.Select(match => match.Line));
    }

    [Fact]
    public void Search_does_not_match_across_lines()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "a\nb");

        Assert.Empty(Run([], Query("a\\nb", regex: true)).Matches);
    }

    [Fact]
    public void Search_counts_regex_occurrences_on_a_line()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "a1 b22 c333\n");

        TextSearchResult result = Run([], Query(@"\d+", regex: true));

        Assert.Equal(3, result.Occurrences);
        TextSearchMatch match = Assert.Single(result.Matches);
        Assert.Equal("a1 b22 c333", match.Text);
    }

    [Fact]
    public void Search_stops_a_regex_at_the_occurrence_limit()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "a a a a\n");

        TextSearchResult result = Run(
            [],
            Query("a", regex: true),
            characterBudget: int.MaxValue,
            occurrenceLimit: 2);

        Assert.Equal(2, result.Occurrences);
        Assert.Single(result.Matches);
    }

    private FileScope Scope() => new(_root, "工作目录", _files);

    private TextSearchResult Run(
        IReadOnlyList<string> paths,
        TextQuery query,
        ToolPathAccess? access = null,
        int characterBudget = TextOutputLimit.Characters,
        int occurrenceLimit = int.MaxValue)
    {
        ErrorOr<TextSearchResult> result = new FileSearch(Scope(), access)
            .Search(paths, query, characterBudget, occurrenceLimit);
        Assert.False(result.IsError);
        return result.Value;
    }

    private static TextQuery Query(
        string pattern,
        IReadOnlyList<string>? extensions = null,
        bool regex = false)
    {
        ErrorOr<TextQuery> query = TextQuery.Create(pattern, extensions, regex);
        Assert.False(query.IsError);
        return query.Value;
    }
}