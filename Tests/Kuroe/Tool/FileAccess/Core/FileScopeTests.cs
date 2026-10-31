using ErrorOr;
using Xunit;

namespace Kuroe.Tool.FileAccess.Tests;

/// <summary>根目录内的文件访问：路径解析、越界拒绝、列目录排序与按范围读取。</summary>
public sealed class FileScopeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Relative_path_resolves_inside_the_root()
    {
        Directory.CreateDirectory(_root);
        FileScope scope = new(_root, "工作目录");

        ErrorOr<string> resolved = scope.Resolve("notes/today.txt");

        Assert.False(resolved.IsError);
        Assert.Equal(Path.Combine(_root, "notes", "today.txt"), resolved.Value);
    }

    [Fact]
    public void Path_outside_the_root_is_rejected()
    {
        FileScope scope = new(_root, "工作目录");

        ErrorOr<string> resolved = scope.Resolve(Path.Combine("..", "escape.txt"));

        Assert.True(resolved.IsError);
        Assert.Contains("工作目录", resolved.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void List_entries_sorts_by_ordinal_name()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "b"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "内容");
        FileScope scope = new(_root, "工作目录");

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = scope.ListEntries(string.Empty, "ReadFile");

        Assert.False(entries.IsError);
        Assert.Equal(["a.txt", "b"], entries.Value.Select(entry => entry.Name));
        Assert.False(entries.Value[0].IsDirectory);
        Assert.True(entries.Value[1].IsDirectory);
    }

    [Fact]
    public void Reading_a_file_as_a_directory_is_rejected()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "内容");
        FileScope scope = new(_root, "工作目录");

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = scope.ListEntries("a.txt", "ReadFile");

        Assert.True(entries.IsError);
        Assert.Contains("ReadFile", entries.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_range_returns_the_requested_lines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "一\n二\n三\n四\n");
        FileScope scope = new(_root, "工作目录");

        ErrorOr<string> text = scope.ReadRange("a.txt", 2, 3, "ListDirectory");

        Assert.False(text.IsError);
        Assert.Equal("二\n三", text.Value);
    }

    [Fact]
    public void Truncation_keeps_the_limit_and_marks_the_tail()
    {
        string text = new('a', 8001);

        string truncated = FileScope.Truncate(text);

        Assert.Equal(8000 + "…内容已截断".Length, truncated.Length);
        Assert.EndsWith("…内容已截断", truncated, StringComparison.Ordinal);
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
