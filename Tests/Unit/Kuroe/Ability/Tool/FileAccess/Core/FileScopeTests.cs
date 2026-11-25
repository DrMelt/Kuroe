using ErrorOr;
using Kuroe.TestSupport;
using Kuroe.Ability.Tool.FileAccess.Output;
using Kuroe.Ability.Tool.FileAccess.Shared;
using Xunit;

namespace Kuroe.Ability.Tool.FileAccess.Tests;

/// <summary>根目录内的文件访问：路径解析、越界拒绝、列目录排序与按范围读取。</summary>
public sealed class FileScopeTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact(DisplayName = "相对路径解析到根目录内")]
    public void Relative_path_resolves_inside_the_root()
    {
        ErrorOr<string> resolved = Scope().Resolve("notes/today.txt");

        Assert.False(resolved.IsError);
        Assert.Equal(Path.Combine(_root, "notes", "today.txt"), resolved.Value);
    }

    [Fact(DisplayName = "根目录之外的路径被拒绝")]
    public void Path_outside_the_root_is_rejected()
    {
        ErrorOr<string> resolved = Scope().Resolve(Path.Combine("..", "escape.txt"));

        Assert.True(resolved.IsError);
        Assert.Contains("工作目录", resolved.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "列条目按序数名排序")]
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

    [Fact(DisplayName = "把文件当作目录读取被拒绝")]
    public void Reading_a_file_as_a_directory_is_rejected()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "内容");

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = Scope().ListEntries("a.txt", "ReadFile");

        Assert.True(entries.IsError);
        Assert.Contains("ReadFile", entries.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "读取范围返回请求的行")]
    public void Read_range_returns_the_requested_lines()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n二\n三\n四\n");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            2,
            3,
            TextOutputLimit.Characters,
            int.MaxValue,
            "ListDirectory");

        Assert.False(read.IsError);
        SourceLine[] expected = [new(2, "二", NewLineStyle.Lf), new(3, "三", NewLineStyle.Lf)];
        Assert.Equal(expected, read.Value.Lines);
        Assert.False(read.Value.ToBeyondEnd);
    }

    [Fact(DisplayName = "没有结束行的读取范围返回全部行")]
    public void Read_range_without_an_end_returns_every_line()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n二\n三\n");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            1,
            null,
            TextOutputLimit.Characters,
            int.MaxValue,
            "ListDirectory");

        Assert.False(read.IsError);
        Assert.Equal(4, read.Value.Lines.Count);
        Assert.True(read.Value.ToBeyondEnd);
    }

    [Fact(DisplayName = "读取范围保留每行终止符")]
    public void Read_range_preserves_each_line_terminator()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\r\n二\r三\n四");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            1,
            10,
            TextOutputLimit.Characters,
            int.MaxValue,
            "ListDirectory");

        Assert.False(read.IsError);
        SourceLine[] expected =
        [
            new(1, "一", NewLineStyle.CrLf),
            new(2, "二", NewLineStyle.Cr),
            new(3, "三", NewLineStyle.Lf),
            new(4, "四", null),
        ];
        Assert.Equal(expected, read.Value.Lines);
        Assert.True(read.Value.ToBeyondEnd);
    }

    [Fact(DisplayName = "读取范围拒绝超出末尾的起始行")]
    public void Read_range_rejects_a_start_line_past_the_end()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            3,
            50,
            TextOutputLimit.Characters,
            int.MaxValue,
            "ListDirectory");

        Assert.True(read.IsError);
        Assert.Contains("超出文件长度", read.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "读取范围拒绝空文件中的起始行")]
    public void Read_range_rejects_a_start_line_in_an_empty_file()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), string.Empty);

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            2,
            50,
            TextOutputLimit.Characters,
            int.MaxValue,
            "ListDirectory");

        Assert.True(read.IsError);
        Assert.Contains("超出文件长度", read.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "读取范围拒绝非正的字符预算")]
    public void Read_range_rejects_a_non_positive_character_budget()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            1,
            1,
            0,
            int.MaxValue,
            "ListDirectory");

        Assert.True(read.IsError);
        Assert.Contains("字符预算", read.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "读取范围拒绝非正的行数预算")]
    public void Read_range_rejects_a_non_positive_line_budget()
    {
        _files.AddFile(Path.Combine(_root, "a.txt"), "一\n");

        ErrorOr<SourceLineReadResult> read = Scope().ReadRange(
            "a.txt",
            1,
            1,
            TextOutputLimit.Characters,
            0,
            "ListDirectory");

        Assert.True(read.IsError);
        Assert.Contains("单行字符预算", read.FirstError.Description, StringComparison.Ordinal);
    }

    private FileScope Scope() => new(_root, "工作目录", _files);
}