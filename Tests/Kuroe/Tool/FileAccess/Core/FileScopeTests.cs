using ErrorOr;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>根目录内的文件访问：路径解析、越界拒绝、列目录排序与按范围读取。</summary>
public sealed class FileScopeTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public void Relative_path_resolves_inside_the_root()
    {
        ErrorOr<string> resolved = Scope().Resolve("notes/today.txt");

        Assert.False(resolved.IsError);
        Assert.Equal(Path.Combine(_root, "notes", "today.txt"), resolved.Value);
    }

    [Fact]
    public void Path_outside_the_root_is_rejected()
    {
        ErrorOr<string> resolved = Scope().Resolve(Path.Combine("..", "escape.txt"));

        Assert.True(resolved.IsError);
        Assert.Contains("工作目录", resolved.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void List_entries_sorts_by_ordinal_name()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "内容");
        _files.AddDirectory(Path.Combine(_root, "b"));

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = Scope().ListEntries(string.Empty, "ReadFile");

        Assert.False(entries.IsError);
        Assert.Equal(["a.txt", "b"], entries.Value.Select(entry => entry.Name));
        Assert.False(entries.Value[0].IsDirectory);
        Assert.True(entries.Value[1].IsDirectory);
    }

    [Fact]
    public void Reading_a_file_as_a_directory_is_rejected()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "内容");

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = Scope().ListEntries("a.txt", "ReadFile");

        Assert.True(entries.IsError);
        Assert.Contains("ReadFile", entries.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_range_returns_the_requested_lines()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n二\n三\n四\n");

        ErrorOr<string> text = Scope().ReadRange("a.txt", 2, 3, "ListDirectory");

        Assert.False(text.IsError);
        Assert.Equal("2 |二\n3 |三", text.Value);
    }

    [Fact]
    public void Read_range_numbers_lines_and_marks_the_file_end()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n二\n三\n四\n");

        ErrorOr<string> text = Scope().ReadRange("a.txt", 2, 10, "ListDirectory");

        Assert.False(text.IsError);
        Assert.Equal("（已到文件结尾，实际返回第 2 到 4 行）\n2 |二\n3 |三\n4 |四", text.Value);
    }

    [Fact]
    public void Read_range_aligns_line_numbers_to_the_last_line()
    {
        _files.AddFile(
            Path.Combine(_root, "a.txt"),
            string.Concat(Enumerable.Range(1, 12).Select(line => $"L{line}\n")));

        ErrorOr<string> text = Scope().ReadRange("a.txt", 8, 12, "ListDirectory");

        Assert.False(text.IsError);
        Assert.Equal(" 8 |L8\n 9 |L9\n10 |L10\n11 |L11\n12 |L12", text.Value);
    }

    [Fact]
    public void Read_range_marks_truncation_before_the_content()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), new string('a', 9000));

        ErrorOr<string> text = Scope().ReadRange("a.txt", 1, 1, "ListDirectory");

        Assert.False(text.IsError);
        const string Notice = "（内容超过 8000 字符，已截断）\n";
        Assert.StartsWith(Notice, text.Value, StringComparison.Ordinal);
        string content = text.Value[Notice.Length..];
        Assert.Equal(8000, content.Length);
        Assert.StartsWith("1 |", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Numbered_lines_mark_truncation_before_the_content()
    {
        string numbered = FileScope.Numbered([new string('a', 9000)], 1);

        const string Notice = "（内容超过 8000 字符，已截断）\n";
        Assert.StartsWith(Notice, numbered, StringComparison.Ordinal);
        Assert.Equal(8000, numbered[Notice.Length..].Length);
    }

    [Fact]
    public void Read_range_reports_an_empty_file()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), string.Empty);

        ErrorOr<string> text = Scope().ReadRange("a.txt", 1, 50, "ListDirectory");

        Assert.False(text.IsError);
        Assert.Equal("（已到文件结尾，文件没有内容）", text.Value);
    }

    private FileScope Scope() => new(_root, "工作目录", _files);
}