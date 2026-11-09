using Kuroe.TestSupport;
using Xunit;

namespace Kuroe.Tool.Source.Tests;

/// <summary>导出资料的查阅工具：列出导出目录内的路径与按行号范围读取原文，路径限制在导出目录内。</summary>
public sealed class SourceToolTests
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));
    private readonly InMemoryWorkspaceFiles _files = new();

    [Fact]
    public void ListSourceDirectory_lists_root_entries()
    {
        _files.AddFile(Path.Combine(_root, "Kuroe", "Tools", "FileTool.cs"), "class A");
        _files.AddFile(Path.Combine(_root, "docs", "usage", "工具扩展.md"), "文档");

        string output = Call("ListSourceDirectory");

        Assert.Contains("导出目录根 的条目（2）", output);
        Assert.Contains("  Kuroe/", output);
        Assert.Contains("  docs/", output);
        Assert.DoesNotContain("FileTool.cs", output);
        Assert.True(
            output.IndexOf("Kuroe/", StringComparison.Ordinal) < output.IndexOf("docs/", StringComparison.Ordinal),
            $"条目按名字序数排序，Kuroe 应排在 docs 之前：\n{output}");
    }

    [Fact]
    public void ListSourceDirectory_lists_subdirectory_entries()
    {
        _files.AddFile(Path.Combine(_root, "Kuroe", "Core", "Kuroe.csproj"), "<Project />");
        _files.AddFile(Path.Combine(_root, "Kuroe", "Core", "Kuroe.cs"), "class A");

        string output = Call("ListSourceDirectory", ("path", "Kuroe/Core"));

        Assert.Contains("Kuroe/Core 的条目（2）", output);
        Assert.Contains("  Kuroe.csproj", output);
        Assert.Contains("  Kuroe.cs", output);
    }

    [Fact]
    public void ListSourceDirectory_reports_empty_directory()
    {
        _files.AddDirectory(_root);

        Assert.Equal("目录 导出目录根 为空。", Call("ListSourceDirectory"));
    }

    [Fact]
    public void ListSourceDirectory_reports_missing_directory()
    {
        Assert.Contains("不存在", Call("ListSourceDirectory"));
    }

    [Fact]
    public void ListSourceDirectory_rejects_file_path()
    {
        _files.AddFile(Path.Combine(_root, "Kuroe.cs"), "class A");

        string output = Call("ListSourceDirectory", ("path", "Kuroe.cs"));

        Assert.Contains("是文件", output);
        Assert.Contains("ReadSourceFile", output);
    }

    [Fact]
    public void ListSourceDirectory_rejects_path_outside_source_directory()
    {
        Assert.Contains("不在导出目录内", Call("ListSourceDirectory", ("path", "..")));
    }

    [Fact]
    public void ListSourceDirectory_truncates_beyond_limit()
    {
        for (int index = 1; index <= 201; index++)
        {
            _files.AddFile(Path.Combine(_root, "Kuroe", $"File{index:000}.cs"), "x");
        }

        string output = Call("ListSourceDirectory", ("path", "Kuroe"));

        Assert.Contains("仅列出前 200 条", output);
        Assert.DoesNotContain("File201.cs", output);
    }

    [Fact]
    public void ReadSourceFile_reads_exact_line_range()
    {
        _files.AddFile(Path.Combine(_root, "Kuroe.cs"), "一\n二\n三\n四\n五\n");

        string output = Call("ReadSourceFile", ("path", "Kuroe.cs"), ("from", "2"), ("to", "4"));

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>2 |二\n3 |三\n4 |四\n<<<KUROE_TEXT_END>>>", output);
    }

    [Fact]
    public void ReadSourceFile_can_omit_line_numbers()
    {
        _files.AddFile(Path.Combine(_root, "raw.cs"), "一\r\n二\r三\n四");

        string output = Call("ReadSourceFile", ("path", "raw.cs"), ("lineNumbers", false));

        Assert.Equal("（已到文件结尾，实际返回第 1 到 4 行）\n<<<KUROE_TEXT_BEGIN>>>一\r\n二\r三\n四<<<KUROE_TEXT_END>>>", output);
    }

    [Fact]
    public void ReadSourceFile_preserves_crlf_line_terminators()
    {
        _files.AddFile(Path.Combine(_root, "Kuroe.cs"), "一\r\n二\r\n");

        Assert.Equal(
            "<<<KUROE_TEXT_BEGIN>>>1 |一\r\n2 |二\r\n<<<KUROE_TEXT_END>>>",
            Call("ReadSourceFile", ("path", "Kuroe.cs"), ("from", "1"), ("to", "2")));
    }

    [Fact]
    public void ReadSourceFile_accepts_absolute_path_inside_source_directory()
    {
        string file = Path.Combine(_root, "Kuroe.cs");
        _files.AddFile(file, "一\n二\n");

        Assert.Equal("<<<KUROE_TEXT_BEGIN>>>2 |二\n<<<KUROE_TEXT_END>>>", Call("ReadSourceFile", ("path", file), ("from", "2"), ("to", "2")));
    }

    [Fact]
    public void ReadSourceFile_accepts_name_starting_with_dots()
    {
        _files.AddFile(Path.Combine(_root, "..foo.cs"), "一\n");

        Assert.Equal("（已到文件结尾，实际返回第 1 到 2 行）\n<<<KUROE_TEXT_BEGIN>>>1 |一\n2 |<<<KUROE_TEXT_END>>>", Call("ReadSourceFile", ("path", "..foo.cs")));
    }

    [Fact]
    public void ReadSourceFile_defaults_to_the_whole_file_and_notes_file_end()
    {
        _files.AddFile(
            Path.Combine(_root, "长文档.md"),
            string.Concat(Enumerable.Range(1, 60).Select(line => $"L{line}\n")));

        string output = Call("ReadSourceFile", ("path", "长文档.md"));

        Assert.StartsWith(
            "（已到文件结尾，实际返回第 1 到 61 行）\n<<<KUROE_TEXT_BEGIN>>> 1 |L1\n 2 |L2\n",
            output,
            StringComparison.Ordinal);
        Assert.Contains("60 |L60\n", output, StringComparison.Ordinal);
        Assert.DoesNotContain("已截断", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSourceFile_truncates_long_content()
    {
        _files.AddFile(Path.Combine(_root, "long.cs"), new string('a', 12000));

        string output = Call("ReadSourceFile", ("path", "long.cs"), ("maxLineChars", "50000"));

        Assert.StartsWith("（内容超过 10000 字符，已截断）\n<<<KUROE_TEXT_BEGIN>>>", output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSourceFile_truncates_a_long_line_by_default()
    {
        _files.AddFile(Path.Combine(_root, "line.cs"), new string('a', 400));

        string output = Call("ReadSourceFile", ("path", "line.cs"), ("to", "1"));

        Assert.StartsWith(
            $"<<<KUROE_TEXT_BEGIN>>>1 |{new string('a', 300)}（本行共 400 字符，已截断）",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSourceFile_accepts_read_limits()
    {
        _files.AddFile(Path.Combine(_root, "short.cs"), "abcdef");

        Assert.Equal(
            "（内容超过 5 字符，已截断）\n<<<KUROE_TEXT_BEGIN>>>1 |ab<<<KUROE_TEXT_END>>>",
            Call("ReadSourceFile", ("path", "short.cs"), ("maxChars", "5"), ("maxLineChars", "100")));
    }

    [Fact]
    public void ReadSourceFile_rejects_invalid_read_limits()
    {
        Assert.Contains(
            "maxChars",
            Call("ReadSourceFile", ("path", "a.cs"), ("maxChars", "50001")),
            StringComparison.Ordinal);
        Assert.Contains(
            "maxLineChars",
            Call("ReadSourceFile", ("path", "a.cs"), ("maxLineChars", "0")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReadSourceFile_rejects_missing_and_illegal_arguments()
    {
        Assert.Contains("缺少参数 path", Call("ReadSourceFile"));
        Assert.Contains("参数 from 要写成数字", Call("ReadSourceFile", ("path", "a.cs"), ("from", "x")));
        Assert.Contains("from 从 1 起", Call("ReadSourceFile", ("path", "a.cs"), ("from", "0")));
        Assert.Contains("to 不能小于起始行号 from", Call(
            "ReadSourceFile", ("path", "a.cs"), ("from", "5"), ("to", "3")));
    }

    [Fact]
    public void ReadSourceFile_rejects_path_outside_source_directory()
    {
        string output = Call("ReadSourceFile", ("path", ".."));

        Assert.Contains("不在导出目录内", output);

        string outside = Path.Combine(Path.GetTempPath(), $"kuroe-source-{Guid.NewGuid():N}.cs");
        Assert.Contains("不在导出目录内", Call("ReadSourceFile", ("path", outside)));
    }

    [Fact]
    public void ReadSourceFile_rejects_directory_and_missing_file()
    {
        _files.AddDirectory(Path.Combine(_root, "Kuroe"));

        string directory = Call("ReadSourceFile", ("path", "Kuroe"));

        Assert.Contains("是目录", directory);
        Assert.Contains("ListSourceDirectory", directory);
        Assert.Contains("不存在", Call("ReadSourceFile", ("path", "Kuroe/nope.cs")));
    }

    [Fact]
    public void ReadSourceFile_declares_path_parameter()
    {
        SourceTool tool = Tool();
        ToolDefinition list = tool.Functions.Single(function => function.Definition.Path.Name.Value == "ListSourceDirectory").Definition;
        ToolDefinition read = tool.Functions.Single(function => function.Definition.Path.Name.Value == "ReadSourceFile").Definition;

        Assert.True(list.Parameters.Single(parameter => parameter.Name.Value == "path").Path);
        Assert.True(read.Parameters.Single(parameter => parameter.Name.Value == "path").Path);
        Assert.False(read.Parameters.Single(parameter => parameter.Name.Value == "from").Path);

        ToolParameter lineNumbers = read.Parameters.Single(parameter => parameter.Name.Value == "lineNumbers");
        Assert.True(lineNumbers.Flag);
        Assert.False(lineNumbers.Required);
    }

    private SourceTool Tool() => new(_root, _files);

    private string Call(string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(Tool().Functions.Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));
}