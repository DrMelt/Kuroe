using System.Text;
using Kuroe.Tool.File;
using Xunit;

namespace Kuroe.Tool.File.Tests;

using File = System.IO.File;

/// <summary>工作目录文件访问工具：读写文本文件与列出目录条目，路径限制在工作目录内。</summary>
public sealed class FileToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    /// <summary>批量读取的路径参数：两个存在的文件。</summary>
    private static readonly string[] _twoFiles = ["a.txt", "b.txt"];

    /// <summary>批量读取的路径参数：一个存在、一个缺失的文件。</summary>
    private static readonly string[] _missingFile = ["a.txt", "missing.txt"];

    /// <summary>批量读取的路径参数：一个存在的文件、一个越界的路径。</summary>
    private static readonly string[] _escapingPath = ["a.txt", ".."];

    [Fact]
    public void ReadFile_defaults_to_head_lines_and_notes_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "hello.txt"), "你好，工作目录！\n第二行");

        string output = Call("ReadFile", ("path", "hello.txt"));

        Assert.Equal("你好，工作目录！\n第二行（已到文件结尾，实际返回 2 行）", output);
    }

    [Fact]
    public void ReadFile_rejects_missing_path_argument()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ReadFile");

        Assert.Contains("缺少参数 path", output);
    }

    [Fact]
    public void ReadFile_rejects_missing_file()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ReadFile", ("path", "nope.txt"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void ReadFile_rejects_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));

        string output = Call("ReadFile", ("path", "dir"));

        Assert.Contains("是目录", output);
        Assert.Contains("ListDirectory", output);
    }

    [Fact]
    public void ReadFile_rejects_path_escaping_working_directory()
    {
        string outside = Path.Combine(Path.GetTempPath(), "kuroe-file-outside.txt");
        File.WriteAllText(outside, "不该被读到");
        try
        {
            Directory.CreateDirectory(_root);
            string relative = Path.Combine(_root, "..", "..", Path.GetFileName(outside));

            string output = Call("ReadFile", ("path", relative));

            Assert.Contains("不在工作目录内", output);
        }
        finally
        {
            File.Delete(outside);
        }
    }

    [Fact]
    public void ReadFile_truncates_long_content()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "long.txt"), new string('a', 9000));

        string output = Call("ReadFile", ("path", "long.txt"));

        Assert.StartsWith(new string('a', 10), output);
        Assert.EndsWith("…内容已截断", output);
        Assert.Equal(8006, output.Length);
    }

    [Fact]
    public void ReadFiles_merges_multiple_files_in_order()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "B内容");

        string output = Call("ReadFiles", ("paths", _twoFiles));

        Assert.Contains("=== a.txt ===", output);
        Assert.Contains("A内容", output);
        Assert.Contains("=== b.txt ===", output);
        Assert.Contains("B内容", output);
        Assert.True(output.IndexOf("A内容", StringComparison.Ordinal) < output.IndexOf("B内容", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadFiles_rejects_empty_or_missing_paths()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("缺少参数 paths", Call("ReadFiles"));
        Assert.Contains("缺少参数 paths", Call("ReadFiles", ("paths", Array.Empty<string>())));
    }

    [Fact]
    public void ReadFiles_keeps_other_entries_when_one_fails()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");

        string output = Call("ReadFiles", ("paths", _missingFile));

        Assert.Contains("A内容", output);
        Assert.Contains("missing.txt 不存在", output);
    }

    [Fact]
    public void ReadFiles_rejects_escaping_entry_before_reading()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");

        string output = Call("ReadFiles", ("paths", _escapingPath));

        Assert.DoesNotContain("A内容", output);
        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void ReadFile_reads_exact_line_range()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "lines.txt"), "一\n二\n三\n四\n五\n");

        string output = Call("ReadFile", ("path", "lines.txt"), ("from", "2"), ("to", "4"));

        Assert.Equal("二\n三\n四", output);
    }

    [Fact]
    public void ReadFile_defaults_to_first_fifty_lines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "many.txt"), string.Concat(Enumerable.Range(1, 60).Select(i => $"L{i}\n")));

        string output = Call("ReadFile", ("path", "many.txt"));

        Assert.StartsWith("L1\nL2\n", output);
        Assert.Contains("L50", output);
        Assert.DoesNotContain("L51", output);
    }

    [Fact]
    public void ReadFile_tolerates_to_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "few.txt"), "一\n二\n");

        string output = Call("ReadFile", ("path", "few.txt"), ("to", "100"));

        Assert.Equal("一\n二（已到文件结尾，实际返回 2 行）", output);
    }

    [Fact]
    public void ReadFile_rejects_from_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "few.txt"), "一\n二\n");

        string output = Call("ReadFile", ("path", "few.txt"), ("from", "10"), ("to", "20"));

        Assert.Contains("超出文件长度", output);
    }

    [Fact]
    public void ReadFile_rejects_to_below_from()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ReadFile", ("path", "a.txt"), ("from", "5"), ("to", "3"));

        Assert.Contains("to 不能小于", output);
    }

    [Fact]
    public void ReadFile_rejects_non_numeric_line_numbers()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("from 要写成数字", Call("ReadFile", ("path", "a.txt"), ("from", "x")));
        Assert.Contains("to 要写成数字", Call("ReadFile", ("path", "a.txt"), ("to", "x")));
    }

    [Fact]
    public void ReadFile_rejects_from_below_one()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ReadFile", ("path", "a.txt"), ("from", "0"));

        Assert.Contains("from 从 1 起", output);
    }

    [Fact]
    public void ListDirectory_lists_root_when_path_omitted()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call("ListDirectory");

        Assert.Contains("条目（2）", output);
        Assert.Contains("  sub/", output);
        Assert.Contains("  a.txt", output);
    }

    [Fact]
    public void ListDirectory_lists_subdirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "inner.txt"), "x");

        string output = Call("ListDirectory", ("path", "sub"));

        Assert.Contains("sub 的条目（1）", output);
        Assert.Contains("  inner.txt", output);
    }

    [Fact]
    public void ListDirectory_rejects_missing_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ListDirectory", ("path", "nope"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void ListDirectory_rejects_file_path()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call("ListDirectory", ("path", "a.txt"));

        Assert.Contains("是文件", output);
        Assert.Contains("ReadFile", output);
    }

    [Fact]
    public void ListDirectory_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call("ListDirectory", ("path", ".."));

        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void WriteFile_creates_file_with_content()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile", ("path", "notes.txt"), ("content", "记下要点\n第二行"));

        Assert.Equal("已写入 8 字符到 notes.txt。", output);
        Assert.Equal("记下要点\n第二行", File.ReadAllText(Path.Combine(_root, "notes.txt")));
    }

    [Fact]
    public void WriteFile_overwrites_existing_file()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "旧内容");

        string output = Call("WriteFile", ("path", "notes.txt"), ("content", "新内容"));

        Assert.Equal("已写入 3 字符到 notes.txt。", output);
        Assert.Equal("新内容", File.ReadAllText(Path.Combine(_root, "notes.txt")));
    }

    [Fact]
    public void WriteFile_creates_missing_parent_directories()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile", ("path", "a/b/c.txt"), ("content", "x"));

        Assert.Equal("已写入 1 字符到 a/b/c.txt。", output);
        Assert.True(File.Exists(Path.Combine(_root, "a", "b", "c.txt")));
    }

    [Fact]
    public void WriteFile_creates_empty_file_with_empty_content()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile", ("path", "empty.txt"), ("content", ""));

        Assert.Equal("已写入 0 字符到 empty.txt。", output);
        Assert.True(File.Exists(Path.Combine(_root, "empty.txt")));
        Assert.Empty(File.ReadAllText(Path.Combine(_root, "empty.txt")));
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
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile", ("path", ".."), ("content", "x"));

        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void WriteFile_rejects_when_target_is_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));

        string output = Call("WriteFile", ("path", "dir"), ("content", "x"));

        Assert.Contains("是目录", output);
    }

    [Fact]
    public void WriteFile_rejects_content_beyond_limit()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile", ("path", "big.txt"), ("content", new string('a', 40001)));

        Assert.Contains("超过上限", output);
        Assert.False(File.Exists(Path.Combine(_root, "big.txt")));
    }

    [Fact]
    public void WriteFile_writes_utf16_with_bom_when_encoding_set()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile",
            ("path", "utf16.txt"), ("content", "内容"), ("encoding", "utf-16"));

        Assert.Equal("已写入 2 字符到 utf16.txt。", output);
        byte[] written = File.ReadAllBytes(Path.Combine(_root, "utf16.txt"));
        Assert.Equal(0xFF, written[0]);
        Assert.Equal(0xFE, written[1]);
        Assert.Equal("内容", Encoding.Unicode.GetString(written, 2, written.Length - 2));
    }

    [Fact]
    public void WriteFile_rejects_unknown_encoding()
    {
        Directory.CreateDirectory(_root);

        string output = Call("WriteFile",
            ("path", "bad.txt"), ("content", "x"), ("encoding", "gbk"));

        Assert.Contains("encoding 只支持", output);
        Assert.False(File.Exists(Path.Combine(_root, "bad.txt")));
    }

    [Fact]
    public void ReadFiles_truncates_when_total_exceeds_limit()
    {
        Directory.CreateDirectory(_root);
        for (int i = 0; i < 5; i++)
        {
            File.WriteAllText(Path.Combine(_root, $"{i}.txt"), new string('x', 9000));
        }

        string[] paths = [.. Enumerable.Range(0, 5).Select(i => $"{i}.txt")];
        string output = Call("ReadFiles", ("paths", paths));

        Assert.Contains("批量读取已截断", output);
        Assert.Contains("=== 0.txt ===", output);
        Assert.DoesNotContain("=== 4.txt ===", output);
    }

    [Fact]
    public void PatchFile_replaces_lines_within_file()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n要被换掉\n三");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "2"),
            ("original", "要被换掉"), ("replacement", "新的二\n新增行"));

        Assert.Equal("已替换 code.txt 的第 2 到 2 行（1 行 → 2 行）。", output);
        Assert.Equal("一\n新的二\n新增行\n三", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_matches_original_in_crlf_file_and_writes_lf()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "win.txt"), "一\r\n二\r\n三");

        string output = Call("PatchFile",
            ("path", "win.txt"), ("from", "2"), ("to", "2"),
            ("original", "二"), ("replacement", "改二"));

        Assert.Equal("已替换 win.txt 的第 2 到 2 行（1 行 → 1 行）。", output);
        Assert.Equal("一\n改二\n三", File.ReadAllText(Path.Combine(_root, "win.txt")));
    }

    [Fact]
    public void PatchFile_writes_utf8_bom_when_encoding_set()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "2"),
            ("original", "二"), ("replacement", "改二"), ("encoding", "utf-8-bom"));

        Assert.Equal("已替换 code.txt 的第 2 到 2 行（1 行 → 1 行）。", output);
        byte[] written = File.ReadAllBytes(Path.Combine(_root, "code.txt"));
        Assert.Equal(0xEF, written[0]);
        Assert.Equal(0xBB, written[1]);
        Assert.Equal(0xBF, written[2]);
    }

    [Fact]
    public void PatchFile_writes_utf16_when_encoding_set()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "2"),
            ("original", "二"), ("replacement", "改二"), ("encoding", "utf-16"));

        Assert.Equal("已替换 code.txt 的第 2 到 2 行（1 行 → 1 行）。", output);
        byte[] written = File.ReadAllBytes(Path.Combine(_root, "code.txt"));
        Assert.Equal(0xFF, written[0]);
        Assert.Equal(0xFE, written[1]);
        Assert.Equal("一\n改二\n三", Encoding.Unicode.GetString(written, 2, written.Length - 2));
    }

    [Fact]
    public void PatchFile_rejects_unknown_encoding()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "2"),
            ("original", "二"), ("replacement", "改二"), ("encoding", "gbk"));

        Assert.Contains("encoding 只支持", output);
        Assert.Equal("一\n二\n三", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_rejects_original_mismatch()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "3"),
            ("original", "写错了"), ("replacement", "新"));

        Assert.Contains("与 original 不一致", output);
        Assert.Contains("二\n三", output);
        Assert.Equal("一\n二\n三", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_deletes_line_range_when_replacement_empty()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三\n四");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "3"),
            ("original", "二\n三"), ("replacement", ""));

        Assert.Equal("已替换 code.txt 的第 2 到 3 行（2 行 → 0 行）。", output);
        Assert.Equal("一\n四", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_rejects_to_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "1"), ("to", "9"),
            ("original", "一\n二"), ("replacement", "x"));

        Assert.Contains("结束行 9 超出", output);
        Assert.Equal("一\n二", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_rejects_from_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二");

        string output = Call("PatchFile",
            ("path", "code.txt"), ("from", "3"), ("to", "5"),
            ("original", "x"), ("replacement", "y"));

        Assert.Contains("起始行 3 超出", output);
    }

    [Fact]
    public void PatchFile_rejects_to_below_from()
    {
        Directory.CreateDirectory(_root);

        string output = Call("PatchFile",
            ("path", "a.txt"), ("from", "5"), ("to", "3"),
            ("original", "x"), ("replacement", "y"));

        Assert.Contains("to 不能小于", output);
    }

    [Fact]
    public void PatchFile_rejects_non_numeric_line_numbers()
    {
        Assert.Contains("参数 from 要写成数字", Call("PatchFile",
            ("path", "a.txt"), ("from", "x"), ("to", "3"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("参数 to 要写成数字", Call("PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "x"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_missing_arguments()
    {
        Assert.Contains("缺少参数 path", Call("PatchFile",
            ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("缺少参数 original", Call("PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "1"), ("replacement", "y")));
        Assert.Contains("缺少参数 replacement", Call("PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "1"), ("original", "x")));
        Assert.Contains("from 从 1 起", Call("PatchFile",
            ("path", "a.txt"), ("to", "1"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("to 从 1 起", Call("PatchFile",
            ("path", "a.txt"), ("from", "1"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_wrong_path()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("不在工作目录内", Call("PatchFile",
            ("path", ".."), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));

        Directory.CreateDirectory(Path.Combine(_root, "dir"));
        Assert.Contains("是目录", Call("PatchFile",
            ("path", "dir"), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));

        Assert.Contains("不存在", Call("PatchFile",
            ("path", "nope.txt"), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_content_beyond_limit()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一");

        string tooLong = new('a', 40001);
        Assert.Contains("original 超过上限", Call("PatchFile",
            ("path", "code.txt"), ("from", "1"), ("to", "1"), ("original", tooLong), ("replacement", "y")));
        Assert.Contains("replacement 超过上限", Call("PatchFile",
            ("path", "code.txt"), ("from", "1"), ("to", "1"), ("original", "一"), ("replacement", tooLong)));

        Assert.Equal("一", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void GetFileInfo_reports_size_and_times()
    {
        Directory.CreateDirectory(_root);
        string target = Path.Combine(_root, "notes.txt");
        File.WriteAllText(target, "记下要点\n第二行");

        string output = Call("GetFileInfo", ("path", "notes.txt"));

        Assert.StartsWith("notes.txt\n", output);
        Assert.Contains($"大小：{new FileInfo(target).Length} 字节", output);
        Assert.Matches(@"创建时间：\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", output);
        Assert.Matches(@"修改时间：\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", output);
        Assert.Matches(@"访问时间：\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", output);
        Assert.Contains("属性：", output);
    }

    [Fact]
    public void GetFileInfo_reports_read_only_attribute()
    {
        Directory.CreateDirectory(_root);
        string target = Path.Combine(_root, "notes.txt");
        File.WriteAllText(target, "x");
        File.SetAttributes(target, FileAttributes.ReadOnly);
        try
        {
            string output = Call("GetFileInfo", ("path", "notes.txt"));

            Assert.Contains("属性：", output);
            Assert.Contains("只读", output);
        }
        finally
        {
            File.SetAttributes(target, FileAttributes.Normal);
        }
    }

    [Fact]
    public void GetFileInfo_rejects_missing_path_argument()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("缺少参数 path", Call("GetFileInfo"));
    }

    [Fact]
    public void GetFileInfo_rejects_missing_file()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("不存在", Call("GetFileInfo", ("path", "nope.txt")));
    }

    [Fact]
    public void GetFileInfo_rejects_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));

        string output = Call("GetFileInfo", ("path", "dir"));

        Assert.Contains("是目录", output);
        Assert.Contains("ListDirectory", output);
    }

    [Fact]
    public void GetFileInfo_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("不在工作目录内", Call("GetFileInfo", ("path", "..")));
    }

    [Fact]
    public void MoveFile_renames_file_in_place()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "old.txt"), "内容");

        string output = Call("MoveFile", ("source", "old.txt"), ("target", "new.txt"));

        Assert.Equal("已把 old.txt 移动到 new.txt。", output);
        Assert.False(File.Exists(Path.Combine(_root, "old.txt")));
        Assert.Equal("内容", File.ReadAllText(Path.Combine(_root, "new.txt")));
    }

    [Fact]
    public void MoveFile_moves_file_and_creates_missing_parent_directory()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");

        string output = Call("MoveFile", ("source", "a.txt"), ("target", "nested/dir/b.txt"));

        Assert.Equal("已把 a.txt 移动到 nested/dir/b.txt。", output);
        Assert.Equal("A内容", File.ReadAllText(Path.Combine(_root, "nested", "dir", "b.txt")));
    }

    [Fact]
    public void MoveFile_renames_directory_with_its_entries()
    {
        Directory.CreateDirectory(Path.Combine(_root, "old"));
        File.WriteAllText(Path.Combine(_root, "old", "a.txt"), "A内容");

        string output = Call("MoveFile", ("source", "old"), ("target", "new"));

        Assert.Equal("已把 old 移动到 new。", output);
        Assert.False(Directory.Exists(Path.Combine(_root, "old")));
        Assert.Equal("A内容", File.ReadAllText(Path.Combine(_root, "new", "a.txt")));
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
        Directory.CreateDirectory(_root);

        Assert.Contains("不存在", Call("MoveFile", ("source", "nope.txt"), ("target", "b.txt")));
        Assert.False(File.Exists(Path.Combine(_root, "b.txt")));
    }

    [Fact]
    public void MoveFile_rejects_existing_target()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "B");

        string output = Call("MoveFile", ("source", "a.txt"), ("target", "b.txt"));

        Assert.Contains("已存在", output);
        Assert.Equal("B", File.ReadAllText(Path.Combine(_root, "b.txt")));
        Assert.Equal("A", File.ReadAllText(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void MoveFile_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        Assert.Contains("不在工作目录内", Call("MoveFile", ("source", ".."), ("target", "b.txt")));
        Assert.Contains("不在工作目录内", Call("MoveFile", ("source", "a.txt"), ("target", "../b.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void MoveFile_rejects_moving_directory_into_itself()
    {
        Directory.CreateDirectory(Path.Combine(_root, "old"));

        string output = Call("MoveFile", ("source", "old"), ("target", "old/sub"));

        Assert.Contains("自身子目录", output);
        Assert.True(Directory.Exists(Path.Combine(_root, "old")));
        Assert.False(Directory.Exists(Path.Combine(_root, "old", "sub")));
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
    public void Face_functions_split_into_read_and_write_groups()
    {
        Dictionary<string, string> paths = Face().SelectMany(tool => tool.Functions)
            .ToDictionary(function => function.Definition.Path.Name.Value, function => function.Definition.Path.Value);

        string[] readFunctions = ["ReadFile", "ReadFiles", "ListDirectory", "GetFileInfo"];
        string[] writeFunctions = ["WriteFile", "PatchFile", "MoveFile"];
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
                ("ReadFiles", "paths"),
                ("ListDirectory", "path"),
                ("GetFileInfo", "path"),
                ("WriteFile", "path"),
                ("PatchFile", "path"),
                ("MoveFile", "source"),
                ("MoveFile", "target"),
            ],
            entry => Assert.True(Parameter(definitions, entry.Item1, entry.Item2).Path));

        Assert.False(Parameter(definitions, "WriteFile", "content").Path);
        Assert.False(Parameter(definitions, "ReadFile", "from").Path);
    }

    /// <summary>文件工具面的全部载体，按函数名取用其中的函数。</summary>
    private IReadOnlyList<ITool> Face() =>
    [
        new ReadFileTool(_root),
        new ListDirectoryTool(_root),
        new ReadFilesTool(_root),
        new WriteFileTool(_root),
        new PatchFileTool(_root),
        new MoveFileTool(_root),
        new GetFileInfoTool(_root),
    ];

    private string Call(string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(Face().SelectMany(tool => tool.Functions)
            .Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));

    /// <summary>按函数名与参数名取参数声明。</summary>
    private static ToolParameter Parameter(IReadOnlyList<ToolDefinition> definitions, string function, string name) =>
        definitions.Single(definition => definition.Path.Name.Value == function)
            .Parameters.Single(parameter => parameter.Name.Value == name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // 临时目录可能已被清理或占用，收尾不再上报
        }
    }
}
