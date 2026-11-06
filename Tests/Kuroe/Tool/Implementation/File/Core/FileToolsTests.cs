using System.Text;
using Kuroe.Flow.Shared.Model;
using Kuroe.Shared;
using Xunit;

namespace Kuroe.Tool.File.Tests;

using File = System.IO.File;

/// <summary>工作目录文件访问工具：读取、搜索、列出目录条目、取文件信息与写入、移动、删除路径，路径限制在工作目录内。</summary>
public sealed class FileToolsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private static readonly string[] stringArray = new[] { ".cs" };

    [Fact]
    public void ReadFile_defaults_to_head_lines_and_notes_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "hello.txt"), "你好，工作目录！\n第二行");

        string output = Call("ReadFile", ("path", "hello.txt"));

        Assert.Equal("（已到文件结尾，实际返回第 1 到 2 行）\n1 |你好，工作目录！\n2 |第二行", output);
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

            Assert.Contains("不在工作区内", output);
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

        Assert.StartsWith("（内容超过 8000 字符，已截断）\n1 |", output, StringComparison.Ordinal);
        Assert.EndsWith(new string('a', 10), output, StringComparison.Ordinal);
        Assert.Equal("（内容超过 8000 字符，已截断）\n".Length + 8000, output.Length);
    }

    [Fact]
    public void ReadFile_reads_exact_line_range()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "lines.txt"), "一\n二\n三\n四\n五\n");

        string output = Call("ReadFile", ("path", "lines.txt"), ("from", "2"), ("to", "4"));

        Assert.Equal("2 |二\n3 |三\n4 |四", output);
    }

    [Fact]
    public void ReadFile_defaults_to_first_fifty_lines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "many.txt"), string.Concat(Enumerable.Range(1, 60).Select(i => $"L{i}\n")));

        string output = Call("ReadFile", ("path", "many.txt"));

        Assert.StartsWith(" 1 |L1\n 2 |L2\n", output, StringComparison.Ordinal);
        Assert.Contains("50 |L50", output, StringComparison.Ordinal);
        Assert.DoesNotContain("51 |L51", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadFile_tolerates_to_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "few.txt"), "一\n二\n");

        string output = Call("ReadFile", ("path", "few.txt"), ("to", "100"));

        Assert.Equal("（已到文件结尾，实际返回第 1 到 2 行）\n1 |一\n2 |二", output);
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
    public void SearchText_groups_matches_by_file_with_numbered_lines()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(
            Path.Combine(_root, "a.txt"),
            string.Join('\n', Enumerable.Range(1, 12).Select(line => line is 3 or 12 ? "needle" : $"L{line}")));
        File.WriteAllText(Path.Combine(_root, "sub", "b.txt"), "one\ntwo\nneedle");

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
        Directory.CreateDirectory(Path.Combine(_root, "Source"));
        Directory.CreateDirectory(Path.Combine(_root, "Tests"));
        File.WriteAllText(Path.Combine(_root, "Source", "a.cs"), "needle");
        File.WriteAllText(Path.Combine(_root, "Source", "b.md"), "needle");
        File.WriteAllText(Path.Combine(_root, "Tests", "c.cs"), "needle");

        string[] paths = ["Source"];
        string output = Call(
            "SearchText",
            ("pattern", "needle"),
            ("paths", paths),
            ("extensions", stringArray));

        Assert.Contains(Path.Combine("Source", "a.cs"), output);
        Assert.DoesNotContain("b.md", output);
        Assert.DoesNotContain(Path.Combine("Tests", "c.cs"), output);
    }

    [Fact]
    public void SearchText_is_case_sensitive_by_default_and_accepts_ignore_case()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "Needle");

        Assert.Contains("没有匹配", Call("SearchText", ("pattern", "needle")));
        Assert.Contains("Needle", Call("SearchText", ("pattern", "needle"), ("ignoreCase", true)));
    }

    [Fact]
    public void SearchText_rejects_blank_pattern()
    {
        Directory.CreateDirectory(_root);

        string output = Call("SearchText", ("pattern", " "));

        Assert.Contains("pattern", output);
    }

    [Fact]
    public void SearchText_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call("SearchText", ("pattern", "x"), ("paths", new[] { ".." }));

        Assert.Contains("不在工作区内", output);
    }

    [Fact]
    public void SearchText_skips_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "needle");

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.Contains("没有可搜索的文件", output);
    }

    [Fact]
    public void SearchText_truncates_output_at_the_character_limit()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "long.txt"), $"needle{new string('a', 9000)}");

        string output = Call("SearchText", ("pattern", "needle"));

        const string Notice = "（内容超过 8000 字符，已截断）\n";
        Assert.StartsWith(Notice, output, StringComparison.Ordinal);
        Assert.Equal(Notice.Length + 8000, output.Length);
    }

    [Fact]
    public void SearchText_keeps_trailing_whitespace_of_matched_lines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "needle \n");

        string output = Call("SearchText", ("pattern", "needle"));

        Assert.EndsWith("1 |needle ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchText_accepts_a_single_path_string()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Source"));
        File.WriteAllText(Path.Combine(_root, "Source", "a.cs"), "needle");
        File.WriteAllText(Path.Combine(_root, "b.md"), "needle");

        string output = Call("SearchText", ("pattern", "needle"), ("paths", "Source"));

        Assert.Contains(Path.Combine("Source", "a.cs"), output);
        Assert.DoesNotContain("b.md", output);
        Assert.Contains("在 Source 中匹配", output);
    }

    [Fact]
    public void SearchText_rejects_a_blank_scalar_path()
    {
        Directory.CreateDirectory(_root);

        string output = Call("SearchText", ("pattern", "x"), ("paths", " "));

        Assert.Contains("paths", output);
        Assert.Contains("不能为空", output);
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

        Assert.Contains("不在工作区内", output);
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

        Assert.Contains("不在工作区内", output);
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
        Assert.Contains("实际内容是：\n2 |二\n3 |三", output);
        Assert.Equal("一\n二\n三", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_numbers_and_aligns_mismatch_content()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "many.txt"), string.Concat(Enumerable.Range(1, 12).Select(line => $"L{line}\n")));

        string output = Call("PatchFile",
            ("path", "many.txt"), ("from", "9"), ("to", "10"),
            ("original", "写错了"), ("replacement", "新"));

        Assert.Contains("实际内容是：\n 9 |L9\n10 |L10", output);
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

        Assert.Contains("不在工作区内", Call("PatchFile",
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

        Assert.Contains("不在工作区内", Call("GetFileInfo", ("path", "..")));
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

        Assert.Contains("不在工作区内", Call("MoveFile", ("source", ".."), ("target", "b.txt")));
        Assert.Contains("不在工作区内", Call("MoveFile", ("source", "a.txt"), ("target", "../b.txt")));
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
    public void DeleteFile_deletes_file()
    {
        Directory.CreateDirectory(_root);
        string target = Path.Combine(_root, "notes.txt");
        File.WriteAllText(target, "内容");

        string output = Call("DeleteFile", ("path", "notes.txt"));

        Assert.Equal("已删除文件 notes.txt。", output);
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void DeleteFile_rejects_missing_path_argument()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("缺少参数 path", Call("DeleteFile"));
    }

    [Fact]
    public void DeleteFile_rejects_missing_path()
    {
        Directory.CreateDirectory(_root);

        string output = Call("DeleteFile", ("path", "nope.txt"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void DeleteFile_deletes_empty_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        string output = Call("DeleteFile", ("path", "empty"));

        Assert.Equal("已删除目录 empty。", output);
        Assert.False(Directory.Exists(Path.Combine(_root, "empty")));
    }

    [Fact]
    public void DeleteFile_rejects_non_empty_directory_without_recursive()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));
        File.WriteAllText(Path.Combine(_root, "dir", "a.txt"), "x");

        string output = Call("DeleteFile", ("path", "dir"));

        Assert.Contains("不为空", output);
        Assert.True(File.Exists(Path.Combine(_root, "dir", "a.txt")));
    }

    [Fact]
    public void DeleteFile_deletes_directory_with_contents_when_recursive()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir", "nested"));
        File.WriteAllText(Path.Combine(_root, "dir", "a.txt"), "x");
        File.WriteAllText(Path.Combine(_root, "dir", "nested", "b.txt"), "y");

        string output = Call("DeleteFile", ("path", "dir"), ("recursive", true));

        Assert.Equal("已删除目录 dir。", output);
        Assert.False(Directory.Exists(Path.Combine(_root, "dir")));
    }

    [Fact]
    public void DeleteFile_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call("DeleteFile", ("path", ".."));

        Assert.Contains("不在工作区内", output);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_itself()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call("DeleteFile", ("path", "."), ("recursive", true));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_with_a_trailing_separator()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call("DeleteFile", ("path", $".{Path.DirectorySeparatorChar}"), ("recursive", true));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_path_base_when_the_root_binding_has_a_trailing_separator()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        DeleteFileTool tool = new(_root + Path.DirectorySeparatorChar);
        string output = ToolResult.Render(tool.Functions.Single().Invoke(
            new ToolArguments(new Dictionary<string, object?> { ["path"] = ".", ["recursive"] = true })));

        Assert.Contains("路径基准目录本身", output);
        Assert.True(File.Exists(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public void DeleteFile_rejects_the_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "密钥");
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "仓库配置");

        Assert.Contains("黑名单", Call("DeleteFile", ("path", ".kuroe/catalog.json")));
        Assert.Contains("黑名单", Call("DeleteFile", ("path", ".git"), ("recursive", true)));
        Assert.True(File.Exists(Path.Combine(_root, ".kuroe", "catalog.json")));
        Assert.True(File.Exists(Path.Combine(_root, ".git", "config")));
    }

    [Fact]
    public void Face_functions_split_into_read_and_write_groups()
    {
        Dictionary<string, string> paths = Face().SelectMany(tool => tool.Functions)
            .ToDictionary(function => function.Definition.Path.Name.Value, function => function.Definition.Path.Value);

        string[] readFunctions = ["ReadFile", "SearchText", "ListDirectory", "GetFileInfo"];
        string[] writeFunctions = ["WriteFile", "PatchFile", "MoveFile", "DeleteFile"];
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
                ("PatchFile", "path"),
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
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "密钥");
        File.WriteAllText(Path.Combine(_root, ".git", "config"), "仓库配置");

        Assert.Contains("黑名单", Call("ReadFile", ("path", ".kuroe/catalog.json")));
        Assert.Contains("黑名单", Call("ReadFile", ("path", ".git/config")));
    }

    [Fact]
    public void ListDirectory_and_GetFileInfo_reject_the_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "密钥");

        Assert.Contains("黑名单", Call("ListDirectory", ("path", ".kuroe")));
        Assert.Contains("黑名单", Call("GetFileInfo", ("path", ".kuroe/catalog.json")));
    }

    [Fact]
    public void Write_Patch_and_Move_reject_the_denied_directories()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "一\n二\n三");

        Assert.Contains("黑名单", Call("WriteFile", ("path", ".git/config"), ("content", "x")));
        Assert.Contains("黑名单", Call("PatchFile",
            ("path", ".kuroe/catalog.json"), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));
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
                ("PatchFile", "path"),
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

        Directory.CreateDirectory(Path.Combine(_root, ".kuroe"));
        File.WriteAllText(Path.Combine(_root, ".kuroe", "catalog.json"), "密钥");

        Assert.Contains("黑名单", Call("ReadFile", ("path", ".KUROE/catalog.json")));
    }

    [Fact]
    public void Explicit_context_reads_the_mapped_root_port()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "mapped.txt"), "映射内容");

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

        string output = ToolResult.Render(new ReadFileTool(context.For("file/read/ReadFile"))
            .Functions.Single()
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["path"] = "mapped.txt" })));

        Assert.Equal("（已到文件结尾，实际返回第 1 行）\n1 |映射内容", output);
    }

    [Fact]
    public void Context_without_a_mapping_rejects_the_tool_call()
    {
        Directory.CreateDirectory(_root);

        ToolContext context = new(
            new FixedPortAccess(new PortName("工作区"), _root),
            new Dictionary<string, IReadOnlyDictionary<string, PortName>>(),
            _root,
            default,
            []);

        string output = ToolResult.Render(new ReadFileTool(context.For("file/read/ReadFile"))
            .Functions.Single()
            .Invoke(new ToolArguments(new Dictionary<string, object?> { ["path"] = "a.txt" })));

        Assert.Contains("路径变量 root 未映射或端口为空", output);
    }

    /// <summary>文件工具面的全部载体，按函数名取用其中的函数。</summary>
    private IReadOnlyList<ITool> Face() =>
    [
        new ReadFileTool(_root),
        new SearchTextTool(_root),
        new ListDirectoryTool(_root),
        new WriteFileTool(_root),
        new PatchFileTool(_root),
        new MoveFileTool(_root),
        new DeleteFileTool(_root),
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
