using ErrorOr;
using Kuroe;
using Kuroe.Configuration;
using Kuroe.Executions.Tools;
using Kuroe.Shared;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Kuroe.Tests;

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

        string output = Call(new FileTool(_root), "ReadFile", ("path", "hello.txt"));

        Assert.Equal("你好，工作目录！\n第二行（已到文件结尾，实际返回 2 行）", output);
    }

    [Fact]
    public void ReadFile_rejects_missing_path_argument()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ReadFile");

        Assert.Contains("缺少参数 path", output);
    }

    [Fact]
    public void ReadFile_rejects_missing_file()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ReadFile", ("path", "nope.txt"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void ReadFile_rejects_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));

        string output = Call(new FileTool(_root), "ReadFile", ("path", "dir"));

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

            string output = Call(new FileTool(_root), "ReadFile", ("path", relative));

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

        string output = Call(new FileTool(_root), "ReadFile", ("path", "long.txt"));

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

        string output = Call(new FileTool(_root), "ReadFiles", ("paths", _twoFiles));

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

        Assert.Contains("至少给一个文件路径", Call(new FileTool(_root), "ReadFiles"));
        Assert.Contains("至少给一个文件路径", Call(new FileTool(_root), "ReadFiles", ("paths", Array.Empty<string>())));
    }

    [Fact]
    public void ReadFiles_keeps_other_entries_when_one_fails()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");

        string output = Call(new FileTool(_root), "ReadFiles", ("paths", _missingFile));

        Assert.Contains("A内容", output);
        Assert.Contains("missing.txt 不存在", output);
    }

    [Fact]
    public void ReadFiles_rejects_escaping_entry()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "A内容");

        string output = Call(new FileTool(_root), "ReadFiles", ("paths", _escapingPath));

        Assert.Contains("A内容", output);
        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void ReadFile_reads_exact_line_range()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "lines.txt"), "一\n二\n三\n四\n五\n");

        string output = Call(new FileTool(_root), "ReadFile", ("path", "lines.txt"), ("from", "2"), ("to", "4"));

        Assert.Equal("二\n三\n四", output);
    }

    [Fact]
    public void ReadFile_defaults_to_first_fifty_lines()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "many.txt"), string.Concat(Enumerable.Range(1, 60).Select(i => $"L{i}\n")));

        string output = Call(new FileTool(_root), "ReadFile", ("path", "many.txt"));

        Assert.StartsWith("L1\nL2\n", output);
        Assert.Contains("L50", output);
        Assert.DoesNotContain("L51", output);
    }

    [Fact]
    public void ReadFile_tolerates_to_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "few.txt"), "一\n二\n");

        string output = Call(new FileTool(_root), "ReadFile", ("path", "few.txt"), ("to", "100"));

        Assert.Equal("一\n二（已到文件结尾，实际返回 2 行）", output);
    }

    [Fact]
    public void ReadFile_rejects_from_beyond_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "few.txt"), "一\n二\n");

        string output = Call(new FileTool(_root), "ReadFile", ("path", "few.txt"), ("from", "10"), ("to", "20"));

        Assert.Contains("超出文件长度", output);
    }

    [Fact]
    public void ReadFile_rejects_to_below_from()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ReadFile", ("path", "a.txt"), ("from", "5"), ("to", "3"));

        Assert.Contains("to 不能小于", output);
    }

    [Fact]
    public void ReadFile_rejects_non_numeric_line_numbers()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("from 要写成数字", Call(new FileTool(_root), "ReadFile", ("path", "a.txt"), ("from", "x")));
        Assert.Contains("to 要写成数字", Call(new FileTool(_root), "ReadFile", ("path", "a.txt"), ("to", "x")));
    }

    [Fact]
    public void ReadFile_rejects_from_below_one()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ReadFile", ("path", "a.txt"), ("from", "0"));

        Assert.Contains("from 从 1 起", output);
    }

    [Fact]
    public void ListDirectory_lists_root_when_path_omitted()
    {
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call(new FileTool(_root), "ListDirectory");

        Assert.Contains("条目（2）", output);
        Assert.Contains("  sub/", output);
        Assert.Contains("  a.txt", output);
    }

    [Fact]
    public void ListDirectory_lists_subdirectory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "inner.txt"), "x");

        string output = Call(new FileTool(_root), "ListDirectory", ("path", "sub"));

        Assert.Contains("sub 的条目（1）", output);
        Assert.Contains("  inner.txt", output);
    }

    [Fact]
    public void ListDirectory_rejects_missing_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ListDirectory", ("path", "nope"));

        Assert.Contains("不存在", output);
    }

    [Fact]
    public void ListDirectory_rejects_file_path()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "x");

        string output = Call(new FileTool(_root), "ListDirectory", ("path", "a.txt"));

        Assert.Contains("是文件", output);
        Assert.Contains("ReadFile", output);
    }

    [Fact]
    public void ListDirectory_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "ListDirectory", ("path", ".."));

        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void WriteFile_creates_file_with_content()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "WriteFile", ("path", "notes.txt"), ("content", "记下要点\n第二行"));

        Assert.Equal("已写入 8 字符到 notes.txt。", output);
        Assert.Equal("记下要点\n第二行", File.ReadAllText(Path.Combine(_root, "notes.txt")));
    }

    [Fact]
    public void WriteFile_overwrites_existing_file()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "notes.txt"), "旧内容");

        string output = Call(new FileTool(_root), "WriteFile", ("path", "notes.txt"), ("content", "新内容"));

        Assert.Equal("已写入 3 字符到 notes.txt。", output);
        Assert.Equal("新内容", File.ReadAllText(Path.Combine(_root, "notes.txt")));
    }

    [Fact]
    public void WriteFile_creates_missing_parent_directories()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "WriteFile", ("path", "a/b/c.txt"), ("content", "x"));

        Assert.Equal("已写入 1 字符到 a/b/c.txt。", output);
        Assert.True(File.Exists(Path.Combine(_root, "a", "b", "c.txt")));
    }

    [Fact]
    public void WriteFile_rejects_missing_arguments()
    {
        Assert.Contains("缺少参数 path", Call(new FileTool(_root), "WriteFile", ("content", "x")));
        Assert.Contains("缺少参数 content", Call(new FileTool(_root), "WriteFile", ("path", "a.txt")));
    }

    [Fact]
    public void WriteFile_rejects_path_escaping_working_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "WriteFile", ("path", ".."), ("content", "x"));

        Assert.Contains("不在工作目录内", output);
    }

    [Fact]
    public void WriteFile_rejects_when_target_is_directory()
    {
        Directory.CreateDirectory(Path.Combine(_root, "dir"));

        string output = Call(new FileTool(_root), "WriteFile", ("path", "dir"), ("content", "x"));

        Assert.Contains("是目录", output);
    }

    [Fact]
    public void WriteFile_rejects_content_beyond_limit()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "WriteFile", ("path", "big.txt"), ("content", new string('a', 40001)));

        Assert.Contains("超过上限", output);
        Assert.False(File.Exists(Path.Combine(_root, "big.txt")));
    }

    [Fact]
    public void PatchFile_replaces_lines_within_file()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n要被换掉\n三");

        string output = Call(new FileTool(_root), "PatchFile",
            ("path", "code.txt"), ("from", "2"), ("to", "2"),
            ("original", "要被换掉"), ("replacement", "新的二\n新增行"));

        Assert.Equal("已替换 code.txt 的第 2 到 2 行（1 行 → 2 行）。", output);
        Assert.Equal("一\n新的二\n新增行\n三", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void PatchFile_matches_original_across_crlf_files()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "win.txt"), "一\r\n二\r\n三");

        string output = Call(new FileTool(_root), "PatchFile",
            ("path", "win.txt"), ("from", "2"), ("to", "2"),
            ("original", "二"), ("replacement", "改二"));

        Assert.Equal("已替换 win.txt 的第 2 到 2 行（1 行 → 1 行）。", output);
        Assert.Equal("一\n改二\n三", File.ReadAllText(Path.Combine(_root, "win.txt")));
    }

    [Fact]
    public void PatchFile_rejects_original_mismatch()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一\n二\n三");

        string output = Call(new FileTool(_root), "PatchFile",
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

        string output = Call(new FileTool(_root), "PatchFile",
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

        string output = Call(new FileTool(_root), "PatchFile",
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

        string output = Call(new FileTool(_root), "PatchFile",
            ("path", "code.txt"), ("from", "3"), ("to", "5"),
            ("original", "x"), ("replacement", "y"));

        Assert.Contains("起始行 3 超出", output);
    }

    [Fact]
    public void PatchFile_rejects_to_below_from()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "5"), ("to", "3"),
            ("original", "x"), ("replacement", "y"));

        Assert.Contains("to 不能小于", output);
    }

    [Fact]
    public void PatchFile_rejects_non_numeric_line_numbers()
    {
        Assert.Contains("from 要写成从 1 起的数字", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "x"), ("to", "3"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("to 要写成从 1 起的数字", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "x"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_missing_arguments()
    {
        Assert.Contains("缺少参数 path", Call(new FileTool(_root), "PatchFile",
            ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("缺少参数 original", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "1"), ("replacement", "y")));
        Assert.Contains("缺少参数 replacement", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "1"), ("to", "1"), ("original", "x")));
        Assert.Contains("from 要写成从 1 起的数字", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("to", "1"), ("original", "x"), ("replacement", "y")));
        Assert.Contains("to 要写成从 1 起的数字", Call(new FileTool(_root), "PatchFile",
            ("path", "a.txt"), ("from", "1"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_wrong_path()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("不在工作目录内", Call(new FileTool(_root), "PatchFile",
            ("path", ".."), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));

        Directory.CreateDirectory(Path.Combine(_root, "dir"));
        Assert.Contains("是目录", Call(new FileTool(_root), "PatchFile",
            ("path", "dir"), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));

        Assert.Contains("不存在", Call(new FileTool(_root), "PatchFile",
            ("path", "nope.txt"), ("from", "1"), ("to", "1"), ("original", "x"), ("replacement", "y")));
    }

    [Fact]
    public void PatchFile_rejects_content_beyond_limit()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "code.txt"), "一");

        string tooLong = new('a', 40001);
        Assert.Contains("original 超过上限", Call(new FileTool(_root), "PatchFile",
            ("path", "code.txt"), ("from", "1"), ("to", "1"), ("original", tooLong), ("replacement", "y")));
        Assert.Contains("replacement 超过上限", Call(new FileTool(_root), "PatchFile",
            ("path", "code.txt"), ("from", "1"), ("to", "1"), ("original", "一"), ("replacement", tooLong)));

        Assert.Equal("一", File.ReadAllText(Path.Combine(_root, "code.txt")));
    }

    [Fact]
    public void File_tools_join_the_tool_face()
    {
        using ServiceProvider provider = Services();

        ToolCollection tools = provider.GetRequiredService<ToolCollection>();

        Assert.Contains(new ToolName("ReadFile"), tools.Names);
        Assert.Contains(new ToolName("ListDirectory"), tools.Names);
        Assert.Contains(new ToolName("ReadFiles"), tools.Names);
        Assert.Contains(new ToolName("WriteFile"), tools.Names);
        Assert.Contains(new ToolName("PatchFile"), tools.Names);
    }

    /// <summary>在临时工作目录里装配整套服务，用于工具面装配断言。</summary>
    private ServiceProvider Services()
    {
        Directory.CreateDirectory(_root);
        var services = new ServiceCollection();
        services.AddLogging();

        ErrorOr<KuroeStartup> startup = services.AddKuroe(KuroePaths.At(_root));
        Assert.False(startup.IsError);

        return services.BuildServiceProvider();
    }

    private static string Call(FileTool tool, string name, params (string Key, object? Value)[] args) =>
        tool.Functions.Single(function => function.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value)));

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
