using Kuroe.Tool.Source;
using Xunit;

namespace Kuroe.Tool.Source.Tests;

/// <summary>导出资料的查阅工具：列出导出目录内的路径与按行号范围读取原文，路径限制在导出目录内。</summary>
public sealed class SourceToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListSourceDirectory_lists_root_entries()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe", "Tools"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "usage"));
        File.WriteAllText(Path.Combine(_root, "Kuroe", "Tools", "FileTool.cs"), "class A");
        File.WriteAllText(Path.Combine(_root, "docs", "usage", "工具扩展.md"), "文档");

        string output = Call(new SourceTool(_root), "ListSourceDirectory");

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
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe", "Core"));
        File.WriteAllText(Path.Combine(_root, "Kuroe", "Core", "Kuroe.csproj"), "<Project />");
        File.WriteAllText(Path.Combine(_root, "Kuroe", "Core", "Kuroe.cs"), "class A");

        string output = Call(new SourceTool(_root), "ListSourceDirectory", ("path", "Kuroe/Core"));

        Assert.Contains("Kuroe/Core 的条目（2）", output);
        Assert.Contains("  Kuroe.csproj", output);
        Assert.Contains("  Kuroe.cs", output);
    }

    [Fact]
    public void ListSourceDirectory_reports_empty_directory()
    {
        Directory.CreateDirectory(_root);

        Assert.Equal("目录 导出目录根 为空。", Call(new SourceTool(_root), "ListSourceDirectory"));
    }

    [Fact]
    public void ListSourceDirectory_reports_missing_directory()
    {
        Assert.Contains("不存在", Call(new SourceTool(_root), "ListSourceDirectory"));
    }

    [Fact]
    public void ListSourceDirectory_rejects_file_path()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Kuroe.cs"), "class A");

        string output = Call(new SourceTool(_root), "ListSourceDirectory", ("path", "Kuroe.cs"));

        Assert.Contains("是文件", output);
        Assert.Contains("ReadSourceFile", output);
    }

    [Fact]
    public void ListSourceDirectory_rejects_path_outside_source_directory()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("不在导出目录内", Call(new SourceTool(_root), "ListSourceDirectory", ("path", "..")));
    }

    [Fact]
    public void ListSourceDirectory_truncates_beyond_limit()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe"));
        for (int index = 1; index <= 201; index++)
        {
            File.WriteAllText(Path.Combine(_root, "Kuroe", $"File{index:000}.cs"), "x");
        }

        string output = Call(new SourceTool(_root), "ListSourceDirectory", ("path", "Kuroe"));

        Assert.Contains("仅列出前 200 条", output);
        Assert.DoesNotContain("File201.cs", output);
    }

    [Fact]
    public void ReadSourceFile_reads_exact_line_range()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "Kuroe.cs"), "一\n二\n三\n四\n五\n");

        string output = Call(new SourceTool(_root), "ReadSourceFile",
            ("path", "Kuroe.cs"), ("from", "2"), ("to", "4"));

        Assert.Equal("二\n三\n四", output);
    }

    [Fact]
    public void ReadSourceFile_accepts_absolute_path_inside_source_directory()
    {
        Directory.CreateDirectory(_root);
        string file = Path.Combine(_root, "Kuroe.cs");
        File.WriteAllText(file, "一\n二\n");

        Assert.Equal("二", Call(new SourceTool(_root), "ReadSourceFile", ("path", file), ("from", "2"), ("to", "2")));
    }

    [Fact]
    public void ReadSourceFile_accepts_name_starting_with_dots()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "..foo.cs"), "一\n");

        Assert.Equal("一（已到文件结尾，实际返回 1 行）",
            Call(new SourceTool(_root), "ReadSourceFile", ("path", "..foo.cs")));
    }

    [Fact]
    public void ReadSourceFile_defaults_to_head_lines_and_notes_file_end()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "短文档.md"), "第一行\n第二行");

        string output = Call(new SourceTool(_root), "ReadSourceFile", ("path", "短文档.md"));

        Assert.Equal("第一行\n第二行（已到文件结尾，实际返回 2 行）", output);
    }

    [Fact]
    public void ReadSourceFile_truncates_long_content()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "long.cs"), new string('a', 9000));

        string output = Call(new SourceTool(_root), "ReadSourceFile", ("path", "long.cs"));

        Assert.EndsWith("…内容已截断", output);
    }

    [Fact]
    public void ReadSourceFile_rejects_missing_and_illegal_arguments()
    {
        Directory.CreateDirectory(_root);

        Assert.Contains("缺少参数 path", Call(new SourceTool(_root), "ReadSourceFile"));
        Assert.Contains("参数 from 要写成数字", Call(new SourceTool(_root), "ReadSourceFile",
            ("path", "a.cs"), ("from", "x")));
        Assert.Contains("from 从 1 起", Call(new SourceTool(_root), "ReadSourceFile",
            ("path", "a.cs"), ("from", "0")));
        Assert.Contains("to 不能小于起始行号 from", Call(new SourceTool(_root), "ReadSourceFile",
            ("path", "a.cs"), ("from", "5"), ("to", "3")));
    }

    [Fact]
    public void ReadSourceFile_rejects_path_outside_source_directory()
    {
        Directory.CreateDirectory(_root);

        string output = Call(new SourceTool(_root), "ReadSourceFile", ("path", ".."));

        Assert.Contains("不在导出目录内", output);

        string outside = Path.Combine(Path.GetTempPath(), $"kuroe-source-{Guid.NewGuid():N}.cs");
        Assert.Contains("不在导出目录内", Call(new SourceTool(_root), "ReadSourceFile", ("path", outside)));
    }

    [Fact]
    public void ReadSourceFile_rejects_directory_and_missing_file()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe"));

        string directory = Call(new SourceTool(_root), "ReadSourceFile", ("path", "Kuroe"));

        Assert.Contains("是目录", directory);
        Assert.Contains("ListSourceDirectory", directory);
        Assert.Contains("不存在", Call(new SourceTool(_root), "ReadSourceFile", ("path", "Kuroe/nope.cs")));
    }

    private static string Call(SourceTool tool, string name, params (string Key, object? Value)[] args) =>
        ToolResult.Render(tool.Functions.Single(function => function.Definition.Path.Name.Value == name)
            .Invoke(new ToolArguments(args.ToDictionary(pair => pair.Key, pair => pair.Value))));

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
