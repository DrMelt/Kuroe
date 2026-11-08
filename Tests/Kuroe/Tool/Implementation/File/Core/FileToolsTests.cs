using System.Text;
using Kuroe.Flow.Shared.Model;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tool.File.Tests;

/// <summary>工作目录文件访问工具：参数解析、路径准入、结果渲染与读写效果。</summary>
public sealed class FileToolsTests
{
    private static readonly string[] CsExtension = [".cs"];
    private static readonly string[] ParentPath = [".."];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public void ReadFile_defaults_to_head_lines_and_notes_file_end()
    {
        _files.AddFile(PathOf("hello.txt"), "你好，工作目录！\n第二行");

        string output = Call("ReadFile", ("path", "hello.txt"));

        Assert.Equal("（已到文件结尾，实际返回第 1 到 2 行）\n1 |你好，工作目录！\n2 |第二行", output);
    }

    [Fact]
    public void ReadFile_rejects_missing_path_argument()
    {
        Assert.Contains("缺少参数 path", Call("ReadFile"));
    }

    [Fact]
    public void ReadFile_rejects_missing_file()
    {
        Assert.Contains("不存在", Call("ReadFile", ("path", "nope.txt")));
    }

    [Fact]
    public void ReadFile_rejects_directory()
    {
        _files.AddDirectory(PathOf("dir"));

        string output = Call("ReadFile", ("path", "dir"));

        Assert.Contains("是目录", output);
        Assert.Contains("ListDirectory", output);
    }

    [Fact]
    public void ReadFile_rejects_path_escaping_working_directory()
    {
        string relative = Path.Combine(_root, "..", "..", "outside.txt");

        string output = Call("ReadFile", ("path", relative));

        Assert.Contains("不在工作区内", output);
    }

    [Fact]
    public void ReadFile_truncates_long_content()
    {
        _files.AddFile(PathOf("long.txt"), new string('a', 9000));

        string output = Call("ReadFile", ("path", "long.txt"));

        Assert.StartsWith("（内容超过 8000 字符，已截断）\n1 |", output, StringComparison.Ordinal);
        Assert.Equal("（内容超过 8000 字符，已截断）\n".Length + 8000, output.Length);
    }

    [Fact]
    public void ReadFile_reads_exact_line_range()
    {
        _files.AddFile(PathOf("lines.txt"), "一\n二\n三\n四\n五\n");

        string output = Call("ReadFile", ("path", "lines.txt"), ("from", "2"), ("to", "4"));

        Assert.Equal("2 |二\n3 |三\n4 |四", output);
    }

    [Fact]
    public void ReadFile_defaults_to_first_fifty_lines()
    {
        _files.AddFile(
            PathOf("many.txt"),
            string.Concat(Enumerable.Range(1, 60).Select(line => $"L{line}\n")));

        string output = Call("ReadFile", ("path", "many.txt"));

        Assert.StartsWith(" 1 |L1\n 2 |L2\n", output, StringComparison.Ordinal);
        Assert.Contains("50 |L50", output, StringComparison.Ordinal);
        Assert.DoesNotContain("51 |L51", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_tolerates_to_beyond_file_end()
    {
        _files.AddFile(PathOf("few.txt"), "一\n二\n");

        string output = Call("ReadFile", ("path", "few.txt"), ("to", "100"));

        Assert.Equal("（已到文件结尾，实际返回第 1 到 2 行）\n1 |一\n2 |二", output);
    }

    [Fact]
    public void ReadFile_rejects_from_beyond_file_end()
    {
        _files.AddFile(PathOf("few.txt"), "一\n二\n");

        string output = Call("ReadFile", ("path", "few.txt"), ("from", "10"), ("to", "20"));

        Assert.Contains("超出文件长度", output);
    }

    [Fact]
    public void ReadFile_rejects_to_below_from()
    {
        string output = Call("ReadFile", ("path", "a.txt"), ("from", "5"), ("to", "3"));

        Assert.Contains("to 不能小于", output);
    }

    [Fact]
    public void ReadFile_rejects_non_numeric_line_numbers()
    {
        Assert.Contains("from 要写成数字", Call("ReadFile", ("path", "a.txt"), ("from", "x")));
        Assert.Contains("to 要写成数字", Call("ReadFile", ("path", "a.txt"), ("to", "x")));
    }

    [Fact]
    public void ReadFile_rejects_from_below_one()
    {
        string output = Call("ReadFile", ("path", "a.txt"), ("from", "0"));

        Assert.Contains("from 从 1 起", output);
    }

    [Fact]
    public void SearchText_groups_matches_by_file_with_numbered_lines()
    {
        _files.AddFile(
            PathOf("a.txt"),
            string.Join('\n', Enumerable.Range(1, 12).Select(line => line is 3 or 12 ? "needle" : $"L{line}")));
        _files.AddFile(PathOf("sub/b.txt"), "one\ntwo\nneedle");

        string output = Call("SearchText", ("pattern", "needle"));

        string expected = string.Join('\n',
            "在 路径基准根 中匹配 3 处：",
            "a.txt",
            " 3 |needle",
            "12 |needle",
            Path.Combine("sub", "b.txt"),
            "3 |needle");
        Assert.Equal(expected, output);
    }

    [Fact]
    public void SearchText_filters_by_scope_and_extension()
    {
        _files.AddFile(PathOf("Source/a.cs"), "needle");
        _files.AddFile(PathOf("Source/b.md"), "needle");
        _files.AddFile(PathOf("Tests/c.cs"), "needle");
        string[] paths = ["Source"];

        string output = Call("SearchText", ("pattern", "needle"), ("paths", paths), ("extensions", CsExtension));

        Assert.Contains(Path.Combine("Source", "a.cs"), output);
        Assert.DoesNotContain("b.md", output);
        Assert.DoesNotContain(Path.Combine("Tests", "c.cs"), output);
    }

    [Fact]
    public void SearchText_is_case_sensitive_by_default_and_accepts_ignore_case()
    {
        _files.AddFile(PathOf("a.txt"), "Needle");

        Assert.Contains("没有匹配", Call("SearchText", ("pattern", "needle")));
        Assert.Contains("Needle", Call("SearchText", ("pattern", "needle"), ("ignoreCase", true)));
    }

    [Fact]
    public void SearchText_rejects_blank_pattern()
    {
        Assert.Contains("pattern", Call("SearchText", ("pattern", " ")));
    }

    [Fact]
    public void SearchText_rejects_path_escaping_working_directory()
    {
        string output = Call("SearchText", ("pattern", "x"), ("paths", ParentPath));

        Assert.Contains("不在工作区内", output);
    }

    [Fact]
    public void SearchText_skips_denied_directories()
    {
        _files.AddFile(PathOf(".kuroe/catalog.json"), "needle");
        _files.AddFile(PathOf("visible.txt"), "needle");

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.Contains("visible.txt", output);
        Assert.DoesNotContain(".kuroe", output);
    }

    [Fact]
    public void SearchText_reports_no_file_when_scope_is_empty()
    {
        _files.AddDirectory(_root);

        Assert.Contains("没有可搜索的文件", Call("SearchText", ("pattern", "needle")));
    }

    [Fact]
    public void SearchText_accepts_a_single_path_string()
    {
        _files.AddFile(PathOf("Source/a.txt"), "needle");
        _files.AddFile(PathOf("b.md"), "needle");

        string output = Call("SearchText", ("pattern", "needle"), ("paths", "Source"));

        Assert.Contains(Path.Combine("Source", "a.txt"), output);
        Assert.DoesNotContain("b.md", output);
        Assert.Contains("在 Source 中匹配", output);
    }

    [Fact]
    public void SearchText_rejects_a_blank_scalar_path()
    {
        string output = Call("SearchText", ("pattern", "needle"), ("paths", " "));

        Assert.Contains("paths", output);
        Assert.Contains("不能为空", output);
    }

    [Fact]
    public void SearchText_truncates_output_at_the_character_limit()
    {
        _files.AddFile(
            PathOf("long.txt"),
            string.Join('\n', Enumerable.Range(1, 12).Select(_ => $"needle{new string('a', 995)}")));

        string output = Call("SearchText", ("pattern", "needle"));

        const string Notice = "（内容超过 8000 字符，已截断）\n";
        Assert.StartsWith(Notice, output, StringComparison.Ordinal);
        Assert.Equal(Notice.Length + 8000, output.Length);
        Assert.Contains("（本行共 1001 字符，展示第 1 至 1000 字符）", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchText_does_not_mark_a_line_at_the_display_limit()
    {
        string line = $"needle{new string('a', 994)}";
        _files.AddFile(PathOf("a.txt"), line);

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.EndsWith($"1 |{line}", output, StringComparison.Ordinal);
        Assert.DoesNotContain("（本行共", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchText_reports_a_match_after_the_display_limit()
    {
        _files.AddFile(PathOf("a.txt"), $"{new string('a', 1000)}needle");

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.Contains("needle", output, StringComparison.Ordinal);
        Assert.Contains("（本行共 1006 字符，展示第 7 至 1006 字符）", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchText_keeps_trailing_whitespace_of_matched_lines()
    {
        _files.AddFile(PathOf("a.txt"), "needle \n");

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.EndsWith("1 |needle ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ListDirectory_lists_root_when_path_omitted()
    {
        _files.AddFile(PathOf("a.txt"), "x");
        _files.AddDirectory(PathOf("b"));

        string output = Call("ListDirectory");

        Assert.Contains("路径基准根 的条目（2）", output);
        Assert.Contains("  a.txt", output);
        Assert.Contains("  b/", output);
    }

    [Fact]
    public void ListDirectory_lists_subdirectory()
    {
        _files.AddFile(PathOf("sub/a.txt"), "x");

        Assert.Contains("  a.txt", Call("ListDirectory", ("path", "sub")));
    }

    [Fact]
    public void ListDirectory_rejects_missing_directory()
    {
        Assert.Contains("不存在", Call("ListDirectory", ("path", "nope")));
    }

    [Fact]
    public void ListDirectory_rejects_file_path()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        string output = Call("ListDirectory", ("path", "a.txt"));

        Assert.Contains("是文件", output);
        Assert.Contains("ReadFile", output);
    }

    [Fact]
    public void ListDirectory_rejects_path_escaping_working_directory()
    {
        Assert.Contains("不在工作区内", Call("ListDirectory", ("path", "..")));
    }

    [Fact]
    public void WriteFile_creates_file_with_content()
    {
        string output = Call("WriteFile", ("path", "notes.txt"), ("content", "记下要点\n第二行"));

        Assert.Equal("已写入 8 字符到 notes.txt。", output);
        Assert.Equal("记下要点\n第二行", ReadText("notes.txt"));
    }

    [Fact]
    public void WriteFile_overwrites_existing_file()
    {
        _files.AddFile(PathOf("notes.txt"), "旧内容");

        string output = Call("WriteFile", ("path", "notes.txt"), ("content", "新内容"));

        Assert.Equal("已写入 3 字符到 notes.txt。", output);
        Assert.Equal("新内容", ReadText("notes.txt"));
    }

    [Fact]
    public void WriteFile_creates_missing_parent_directories()
    {
        string output = Call("WriteFile", ("path", "a/b/c.txt"), ("content", "x"));

        Assert.Equal("已写入 1 字符到 a/b/c.txt。", output);
        Assert.True(_files.FileExists(PathOf("a/b/c.txt")));
    }

    [Fact]
    public void WriteFile_creates_empty_file_with_empty_content()
    {
        string output = Call("WriteFile", ("path", "empty.txt"), ("content", ""));

        Assert.Equal("已写入 0 字符到 empty.txt。", output);
        Assert.True(_files.FileExists(PathOf("empty.txt")));
        Assert.Empty(ReadText("empty.txt"));
    }

    [Fact]
    public void WriteFile_rejects_missing_arguments()
    {
        Assert.Contains("缺少参数 path", Call("WriteFile", ("content", "x")));
        Assert.Contains("缺少参数 content", Call("WriteFile", ("path", "a.txt")));
    }

    [Fact]
    public void WriteFile_rejects_path_escaping_working_directory()
    {
        string output = Call("WriteFile", ("path", ".."), ("content", "x"));

        Assert.Contains("不在工作区内", output);
    }

    [Fact]
    public void WriteFile_rejects_when_target_is_directory()
    {
        _files.AddDirectory(PathOf("dir"));

        string output = Call("WriteFile", ("path", "dir"), ("content", "x"));

        Assert.Contains("是目录", output);
    }

    [Fact]
    public void WriteFile_rejects_content_beyond_limit()
    {
        string output = Call("WriteFile", ("path", "big.txt"), ("content", new string('a', 40001)));

        Assert.Contains("超过上限", output);
        Assert.False(_files.FileExists(PathOf("big.txt")));
    }

    [Fact]
    public void WriteFile_writes_utf16_with_bom_when_encoding_set()
    {
        string output = Call("WriteFile", ("path", "utf16.txt"), ("content", "内容"), ("encoding", "utf-16"));

        Assert.Equal("已写入 2 字符到 utf16.txt。", output);
        byte[] written = ReadBytes("utf16.txt");
        Assert.Equal(0xFF, written[0]);
        Assert.Equal(0xFE, written[1]);
        Assert.Equal("内容", Encoding.Unicode.GetString(written, 2, written.Length - 2));
    }

    [Fact]
    public void WriteFile_rejects_unknown_encoding()
    {
        string output = Call("WriteFile", ("path", "bad.txt"), ("content", "x"), ("encoding", "gbk"));

        Assert.Contains("encoding 只支持", output);
        Assert.False(_files.FileExists(PathOf("bad.txt")));
    }

    [Fact]
    public void GetFileInfo_reports_size_and_times()
    {
        string target = PathOf("notes.txt");
        _files.AddFile(target, "x");
        DateTimeOffset at = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        _files.SetFacts(target, new FileFacts(42, at, at, at, FileAttributes.Archive));

        string output = Call("GetFileInfo", ("path", "notes.txt"));

        Assert.StartsWith("notes.txt\n", output);
        Assert.Contains("大小：42 字节", output);
        Assert.Contains("创建时间：2026-01-02 03:04:05", output);
        Assert.Contains("修改时间：2026-01-02 03:04:05", output);
        Assert.Contains("访问时间：2026-01-02 03:04:05", output);
        Assert.Contains("属性：归档", output);
    }

    [Fact]
    public void GetFileInfo_reports_read_only_attribute()
    {
        string target = PathOf("notes.txt");
        _files.AddFile(target, "x");
        DateTimeOffset at = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        _files.SetFacts(target, new FileFacts(1, at, at, at, FileAttributes.ReadOnly));

        string output = Call("GetFileInfo", ("path", "notes.txt"));

        Assert.Contains("属性：", output);
        Assert.Contains("只读", output);
    }

    [Fact]
    public void GetFileInfo_rejects_missing_path_argument()
    {
        Assert.Contains("缺少参数 path", Call("GetFileInfo"));
    }

    [Fact]
    public void GetFileInfo_rejects_missing_file()
    {
        Assert.Contains("不存在", Call("GetFileInfo", ("path", "nope.txt")));
    }

    [Fact]
    public void GetFileInfo_rejects_directory()
    {
        _files.AddDirectory(PathOf("dir"));

        string output = Call("GetFileInfo", ("path", "dir"));

        Assert.Contains("是目录", output);
        Assert.Contains("ListDirectory", output);
    }

    [Fact]
    public void GetFileInfo_rejects_path_escaping_working_directory()
    {
        Assert.Contains("不在工作区内", Call("GetFileInfo", ("path", "..")));
    }

    [Fact]
    public void MoveFile_renames_file_in_place()
    {
        _files.AddFile(PathOf("old.txt"), "内容");

        string output = Call("MoveFile", ("source", "old.txt"), ("target", "new.txt"));

        Assert.Equal("已把 old.txt 移动到 new.txt。", output);
        Assert.False(_files.FileExists(PathOf("old.txt")));
        Assert.Equal("内容", ReadText("new.txt"));
    }

    [Fact]
    public void MoveFile_moves_file_and_creates_missing_parent_directory()
    {
        _files.AddFile(PathOf("a.txt"), "A内容");

        string output = Call("MoveFile", ("source", "a.txt"), ("target", "nested/dir/b.txt"));

        Assert.Equal("已把 a.txt 移动到 nested/dir/b.txt。", output);
        Assert.Equal("A内容", ReadText("nested/dir/b.txt"));
    }

    [Fact]
    public void MoveFile_renames_directory_with_its_entries()
    {
        _files.AddFile(PathOf("old/a.txt"), "A内容");

        string output = Call("MoveFile", ("source", "old"), ("target", "new"));

        Assert.Equal("已把 old 移动到 new。", output);
        Assert.False(_files.DirectoryExists(PathOf("old")));
        Assert.Equal("A内容", ReadText("new/a.txt"));
    }

    [Fact]
    public void MoveFile_rejects_missing_arguments()
    {
        Assert.Contains("缺少参数 source", Call("MoveFile", ("target", "b.txt")));
        Assert.Contains("缺少参数 target", Call("MoveFile", ("source", "a.txt")));
    }

    [Fact]
    public void MoveFile_rejects_missing_source()
    {
        Assert.Contains("不存在", Call("MoveFile", ("source", "nope.txt"), ("target", "b.txt")));
        Assert.False(_files.FileExists(PathOf("b.txt")));
    }

    [Fact]
    public void MoveFile_rejects_existing_target()
    {
        _files.AddFile(PathOf("a.txt"), "A");
        _files.AddFile(PathOf("b.txt"), "B");

        string output = Call("MoveFile", ("source", "a.txt"), ("target", "b.txt"));

        Assert.Contains("已存在", output);
        Assert.Equal("B", ReadText("b.txt"));
        Assert.Equal("A", ReadText("a.txt"));
    }

    [Fact]
    public void MoveFile_rejects_path_escaping_working_directory()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        Assert.Contains("不在工作区内", Call("MoveFile", ("source", ".."), ("target", "b.txt")));
        Assert.Contains("不在工作区内", Call("MoveFile", ("source", "a.txt"), ("target", "../b.txt")));
        Assert.True(_files.FileExists(PathOf("a.txt")));
    }

    [Fact]
    public void MoveFile_rejects_moving_directory_into_itself()
    {
        _files.AddDirectory(PathOf("old"));

        string output = Call("MoveFile", ("source", "old"), ("target", "old/sub"));

        Assert.Contains("自身子目录", output);
        Assert.True(_files.DirectoryExists(PathOf("old")));
        Assert.False(_files.DirectoryExists(PathOf("old/sub")));
    }

    [Fact]
    public void MoveFile_description_hints_rename()
    {
        ToolDefinition definition = Face().SelectMany(tool => tool.Functions)
            .Single(function => function.Definition.Path.Value == "file/write/MoveFile")
            .Definition;

        Assert.Contains("重命名", definition.Description);
    }

    [Fact]
    public void DeleteFile_deletes_file()
    {
        _files.AddFile(PathOf("notes.txt"), "x");

        string output = Call("DeleteFile", ("path", "notes.txt"));

        Assert.Equal("已删除文件 notes.txt。", output);
        Assert.False(_files.FileExists(PathOf("notes.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_missing_path_argument()
    {
        Assert.Contains("缺少参数 path", Call("DeleteFile"));
    }

    [Fact]
    public void DeleteFile_rejects_missing_path()
    {
        string output = Call("DeleteFile", ("path", "nope.txt"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void DeleteFile_deletes_empty_directory()
    {
        _files.AddDirectory(PathOf("empty"));

        string output = Call("DeleteFile", ("path", "empty"));

        Assert.Equal("已删除目录 empty。", output);
        Assert.False(_files.DirectoryExists(PathOf("empty")));
    }

    [Fact]
    public void DeleteFile_rejects_non_empty_directory_without_recursive()
    {
        _files.AddFile(PathOf("dir/a.txt"), "x");

        string output = Call("DeleteFile", ("path", "dir"));

        Assert.Contains("不为空", output);
        Assert.True(_files.FileExists(PathOf("dir/a.txt")));
    }

    [Fact]
    public void DeleteFile_deletes_directory_with_contents_when_recursive()
    {
        _files.AddFile(PathOf("dir/a.txt"), "x");
        _files.AddFile(PathOf("dir/nested/b.txt"), "y");

        string output = Call("DeleteFile", ("path", "dir"), ("recursive", true));

        Assert.Equal("已删除目录 dir。", output);
        Assert.False(_files.DirectoryExists(PathOf("dir")));
    }

    [Fact]
    public void DeleteFile_rejects_path_escaping_working_directory()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        string output = Call("DeleteFile", ("path", ".."));

        Assert.Contains("不在工作区内", output);
        Assert.True(_files.FileExists(PathOf("a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_itself()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        string output = Call("DeleteFile", ("path", "."), ("recursive", true));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(_files.FileExists(PathOf("a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_with_a_trailing_separator()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        string output = Call("DeleteFile", ("path", $".{Path.DirectorySeparatorChar}"), ("recursive", true));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(_files.FileExists(PathOf("a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_when_the_root_binding_has_a_trailing_separator()
    {
        _files.AddFile(PathOf("a.txt"), "x");

        DeleteFileTool tool = new(_root + Path.DirectorySeparatorChar, _files);
        string output = ToolResult.Render(tool.Functions.Single().Invoke(
            new ToolArguments(new Dictionary<string, object?> { ["path"] = ".", ["recursive"] = true })));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(_files.FileExists(PathOf("a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_denied_directories()
    {
        _files.AddFile(PathOf(".kuroe/catalog.json"), "密钥");
        _files.AddFile(PathOf(".git/config"), "仓库配置");

        Assert.Contains("黑名单", Call("DeleteFile", ("path", ".kuroe/catalog.json")));
        Assert.Contains("黑名单", Call("DeleteFile", ("path", ".git"), ("recursive", true)));
        Assert.True(_files.FileExists(PathOf(".kuroe/catalog.json")));
        Assert.True(_files.FileExists(PathOf(".git/config")));
    }

    [Fact]
    public void ReplaceText_replaces_across_files_and_reports_lines()
    {
        _files.AddFile(PathOf("a.txt"), "needle\nneedle");
        _files.AddFile(PathOf("sub/b.txt"), "needle");
        string[] paths = ["."];
        string[] extensions = ["txt"];

        string output = Call(
            "ReplaceText",
            ("paths", paths),
            ("pattern", "needle"),
            ("replacement", "x"),
            ("extensions", extensions));

        Assert.Contains("已替换 2 个文件的 3 处文本", output);
        Assert.Contains("a.txt：2 处，第 1、2 行", output);
        Assert.Contains(Path.Combine("sub", "b.txt") + "：1 处，第 1 行", output);
        Assert.Equal("x\nx", ReadText("a.txt"));
        Assert.Equal("x", ReadText("sub/b.txt"));
    }

    [Fact]
    public void ReplaceText_dry_run_does_not_write()
    {
        string file = PathOf("a.txt");
        _files.AddFile(file, "needle");
        string[] paths = ["."];

        string output = Call(
            "ReplaceText",
            ("paths", paths),
            ("pattern", "needle"),
            ("replacement", "x"),
            ("dryRun", true));

        Assert.Contains("将替换 1 个文件的 1 处文本", output);
        Assert.Equal("needle", ReadText("a.txt"));
    }

    [Fact]
    public void ReplaceText_accepts_a_single_path_string()
    {
        _files.AddFile(PathOf("Source/a.txt"), "needle");
        _files.AddFile(PathOf("b.txt"), "needle");

        string output = Call("ReplaceText", ("paths", "Source"), ("pattern", "needle"), ("replacement", "x"));

        Assert.Contains("已替换 1 个文件的 1 处文本", output);
        Assert.Equal("x", ReadText("Source/a.txt"));
        Assert.Equal("needle", ReadText("b.txt"));
    }

    [Fact]
    public void ReplaceText_reports_one_line_for_multiple_matches_on_the_same_line()
    {
        _files.AddFile(PathOf("a.txt"), "needle needle");
        string[] paths = ["."];

        string output = Call("ReplaceText", ("paths", paths), ("pattern", "needle"), ("replacement", "x"));

        Assert.Contains("已替换 1 个文件的 2 处文本", output);
        Assert.Contains("a.txt：2 处，第 1 行", output);
    }

    [Fact]
    public void ReplaceText_writes_utf16_with_bom_when_encoding_set()
    {
        string file = PathOf("a.txt");
        _files.AddFile(file, "needle");
        string[] paths = ["."];

        string output = Call(
            "ReplaceText",
            ("paths", paths),
            ("pattern", "needle"),
            ("replacement", "替换"),
            ("encoding", "utf-16"));

        Assert.Contains("已替换 1 个文件的 1 处文本", output);
        byte[] bytes = ReadBytes("a.txt");
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Equal("替换", Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void ReplaceText_rejects_missing_paths()
    {
        string output = Call("ReplaceText", ("pattern", "x"), ("replacement", "y"));

        Assert.Contains("缺少参数 paths", output);
    }

    [Fact]
    public void ReplaceText_rejects_blank_pattern_and_unknown_encoding()
    {
        string[] paths = ["."];

        Assert.Contains("pattern", Call("ReplaceText", ("paths", paths), ("pattern", " "), ("replacement", "y")));
        Assert.Contains(
            "encoding 只支持",
            Call("ReplaceText", ("paths", paths), ("pattern", "x"), ("replacement", "y"), ("encoding", "gbk")));
    }

    [Fact]
    public void ReplaceText_reports_no_match()
    {
        _files.AddFile(PathOf("a.txt"), "other");
        string[] paths = ["."];

        string output = Call("ReplaceText", ("paths", paths), ("pattern", "needle"), ("replacement", "x"));

        Assert.Contains("没有匹配", output);
        Assert.Equal("other", ReadText("a.txt"));
    }

    [Fact]
    public void ReplaceText_rejects_the_denied_directories()
    {
        _files.AddFile(PathOf(".kuroe/catalog.json"), "needle");
        string[] paths = [".kuroe/catalog.json"];

        string output = Call("ReplaceText", ("paths", paths), ("pattern", "needle"), ("replacement", "x"));

        Assert.Contains("黑名单", output);
    }

    [Fact]
    public void ReplaceText_declares_paths_as_a_required_path_list()
    {
        IReadOnlyList<ToolDefinition> definitions =
            [.. Face().SelectMany(tool => tool.Functions).Select(function => function.Definition)];

        ToolParameter paths = Parameter(definitions, "ReplaceText", "paths");

        Assert.True(paths.Required);
        Assert.True(paths.List);
        Assert.True(paths.Path);
        Assert.False(Parameter(definitions, "ReplaceText", "replacement").Path);
    }

    [Fact]
    public void Face_functions_split_into_read_and_write_groups()
    {
        Dictionary<string, string> paths = Face().SelectMany(tool => tool.Functions)
            .ToDictionary(function => function.Definition.Path.Name.Value, function => function.Definition.Path.Value);

        string[] readFunctions = ["ReadFile", "SearchText", "ListDirectory", "GetFileInfo"];
        string[] writeFunctions = ["WriteFile", "ReplaceText", "MoveFile", "DeleteFile"];
        foreach (string name in readFunctions)
        {
            Assert.Equal($"file/read/{name}", paths[name]);
        }

        foreach (string name in writeFunctions)
        {
            Assert.Equal($"file/write/{name}", paths[name]);
        }
    }

    [Fact]
    public void Face_declares_path_parameters()
    {
        IReadOnlyList<ToolDefinition> definitions =
            [.. Face().SelectMany(tool => tool.Functions).Select(function => function.Definition)];

        Assert.All(
            [
                ("ReadFile", "path"),
                ("SearchText", "paths"),
                ("ListDirectory", "path"),
                ("GetFileInfo", "path"),
                ("WriteFile", "path"),
                ("ReplaceText", "paths"),
                ("MoveFile", "source"),
                ("MoveFile", "target"),
                ("DeleteFile", "path"),
            ],
            entry => Assert.True(Parameter(definitions, entry.Item1, entry.Item2).Path));

        Assert.False(Parameter(definitions, "WriteFile", "content").Path);
        Assert.False(Parameter(definitions, "ReadFile", "from").Path);
    }

    [Fact]
    public void DeleteFile_declares_recursive_as_an_optional_switch()
    {
        IReadOnlyList<ToolDefinition> definitions =
            [.. Face().SelectMany(tool => tool.Functions).Select(function => function.Definition)];

        ToolParameter recursive = Parameter(definitions, "DeleteFile", "recursive");

        Assert.True(recursive.Flag);
        Assert.False(recursive.Required);
    }

    [Fact]
    public void ReadFile_rejects_kuroe_and_git_directories()
    {
        _files.AddFile(PathOf(".kuroe/catalog.json"), "密钥");
        _files.AddFile(PathOf(".git/config"), "仓库配置");

        Assert.Contains("黑名单", Call("ReadFile", ("path", ".kuroe/catalog.json")));
        Assert.Contains("黑名单", Call("ReadFile", ("path", ".git/config")));
    }

    [Fact]
    public void ListDirectory_and_GetFileInfo_reject_the_denied_directories()
    {
        _files.AddFile(PathOf(".kuroe/catalog.json"), "密钥");

        Assert.Contains("黑名单", Call("ListDirectory", ("path", ".kuroe")));
        Assert.Contains("黑名单", Call("GetFileInfo", ("path", ".kuroe/catalog.json")));
    }

    [Fact]
    public void Write_and_Move_reject_the_denied_directories()
    {
        _files.AddFile(PathOf("a.txt"), "一\n二\n三");

        Assert.Contains("黑名单", Call("WriteFile", ("path", ".git/config"), ("content", "x")));
        Assert.Contains("黑名单", Call("MoveFile", ("source", "a.txt"), ("target", ".kuroe/a.txt")));
        Assert.Contains("黑名单", Call("MoveFile", ("source", ".git/config"), ("target", "b.txt")));
    }

    [Fact]
    public void Face_declares_the_same_deny_entries_on_every_path_parameter()
    {
        IReadOnlyList<ToolDefinition> definitions =
            [.. Face().SelectMany(tool => tool.Functions).Select(function => function.Definition)];

        Assert.All(
            [
                ("ReadFile", "path"),
                ("SearchText", "paths"),
                ("ListDirectory", "path"),
                ("GetFileInfo", "path"),
                ("WriteFile", "path"),
                ("ReplaceText", "paths"),
                ("MoveFile", "source"),
                ("MoveFile", "target"),
                ("DeleteFile", "path"),
            ],
            entry =>
            {
                ToolPathAccess access =
                    Assert.IsType<ToolPathAccess>(Parameter(definitions, entry.Item1, entry.Item2).PathAccess);
                Assert.Equal([".kuroe", ".git"], access.Deny);
            });
    }

    /// <summary>大小写变体在 Windows 上按路径语义同样拒绝。</summary>
    [Fact]
    public void Denied_paths_are_case_insensitive_on_windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Contains("黑名单", Call("ReadFile", ("path", ".KUROE/catalog.json")));
    }

    [Fact]
    public void Explicit_context_reads_the_mapped_root_port()
    {
        _files.AddFile(PathOf("mapped.txt"), "映射内容");

        PortName port = new("工作区");
        ToolContext context = new(
            new FixedPortAccess(port, _root),
            new Dictionary<string, IReadOnlyDictionary<string, PortName>>
            {
                ["file/read/ReadFile"] = new Dictionary<string, PortName> { [PathVariables.RootName] = port },
            },
            _root,
            default,
            [new ToolPath("file/read/ReadFile")]);

        string output = ToolResult.Render(new ReadFileTool(context.For("file/read/ReadFile"), _files)
            .Functions.Single()
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["path"] = "mapped.txt" })));

        Assert.Equal("（已到文件结尾，实际返回第 1 行）\n1 |映射内容", output);
    }

    [Fact]
    public void Context_without_a_mapping_rejects_the_tool_call()
    {
        ToolContext context = new(
            new FixedPortAccess(new PortName("工作区"), _root),
            new Dictionary<string, IReadOnlyDictionary<string, PortName>>(),
            _root,
            default,
            []);

        string output = ToolResult.Render(new ReadFileTool(context.For("file/read/ReadFile"), _files)
            .Functions.Single()
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["path"] = "a.txt" })));

        Assert.Contains("路径变量 root 未映射或端口为空", output);
    }

    private string PathOf(string relative) => Path.Combine(_root, relative);

    private string ReadText(string relative) => _files.ReadText(PathOf(relative));

    private byte[] ReadBytes(string relative) => _files.ReadBytes(PathOf(relative));

    /// <summary>文件工具面的全部载体，按函数名取用其中的函数。</summary>
    private IReadOnlyList<ITool> Face() =>
    [
        new ReadFileTool(_root, _files),
        new SearchTextTool(_root, _files),
        new ListDirectoryTool(_root, _files),
        new WriteFileTool(_root, _files),
        new ReplaceTextTool(_root, _files),
        new MoveFileTool(_root, _files),
        new DeleteFileTool(_root, _files),
        new GetFileInfoTool(_root, _files),
    ];

    private string Call(string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(Face().SelectMany(tool => tool.Functions)
            .Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));

    /// <summary>按函数名与参数名取参数声明。</summary>
    private static ToolParameter Parameter(IReadOnlyList<ToolDefinition> definitions, string function, string name) =>
        definitions.Single(definition => definition.Path.Name.Value == function)
            .Parameters.Single(parameter => parameter.Name.Value == name);
}