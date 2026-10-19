using Kuroe.Tool.Source;
using Xunit;

namespace Kuroe.Tool.Source.Tests;

/// <summary>导出资料的查阅工具：列出导出目录内的路径与按行号范围读取原文，路径限制在导出目录内。</summary>
public sealed class SourceToolTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "kuroe-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ListSourceFiles_lists_relative_paths()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe", "Tools"));
        Directory.CreateDirectory(Path.Combine(_root, "docs", "usage"));
        File.WriteAllText(Path.Combine(_root, "Kuroe", "Tools", "FileTool.cs"), "class A");
        File.WriteAllText(Path.Combine(_root, "docs", "usage", "工具扩展.md"), "文档");

        string output = Call(new SourceTool(_root), "ListSourceFiles");

        Assert.Contains("文件 2 个", output);
        Assert.Contains("  Kuroe/Tools/FileTool.cs", output);
        Assert.Contains("  docs/usage/工具扩展.md", output);
        Assert.True(
            output.IndexOf("Kuroe/Tools/FileTool.cs", StringComparison.Ordinal)
            < output.IndexOf("docs/usage/工具扩展.md", StringComparison.Ordinal),
            $"路径按序数排序，Kuroe 应排在 docs 之前：\n{output}");
    }

    [Fact]
    public void ListSourceFiles_reports_empty_directory()
    {
        Directory.CreateDirectory(_root);

        Assert.Equal("导出目录内没有文件。", Call(new SourceTool(_root), "ListSourceFiles"));
    }

    [Fact]
    public void ListSourceFiles_reports_missing_directory()
    {
        Assert.Contains("导出目录不存在", Call(new SourceTool(_root), "ListSourceFiles"));
    }

    [Fact]
    public void ListSourceFiles_truncates_beyond_limit()
    {
        Directory.CreateDirectory(Path.Combine(_root, "Kuroe"));
        for (int index = 1; index <= 401; index++)
        {
            File.WriteAllText(Path.Combine(_root, "Kuroe", $"File{index:000}.cs"), "x");
        }

        string output = Call(new SourceTool(_root), "ListSourceFiles");

        Assert.Contains("仅列出前 400 条", output);
        Assert.DoesNotContain("File401.cs", output);
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
        Assert.Contains("ListSourceFiles", directory);
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
