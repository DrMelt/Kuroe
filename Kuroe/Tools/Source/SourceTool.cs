using System.Text;
using ErrorOr;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Tools.Files;

namespace Kuroe.Tools.Source;

/// <summary>导出资料的查阅工具：列出程序随附的源码与文档路径，按行号范围读取原文。
/// 只读，路径解析后必须落回导出目录内。</summary>
internal sealed class SourceTool(string sourceDirectory) : ITool
{
    /// <summary>ReadSourceFile 缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    /// <summary>文件清单的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 400;

    /// <summary>导出资料工具的分组。</summary>
    private static readonly ToolPath Group = new("source");

    /// <summary>列出导出目录内的文件。</summary>
    private static readonly ToolDefinition ListSourceFiles = new(
        new ToolName("ListSourceFiles"),
        "列出随程序导出的源码与文档路径，路径相对导出目录。内容是程序自身实现，用于分析实现方法。",
        [],
        Group);

    /// <summary>读取导出目录内文件指定行号范围。</summary>
    private static readonly ToolDefinition ReadSourceFile = new(
        new ToolName("ReadSourceFile"),
        "读取导出目录内源码或文档指定行号范围的内容。from 与 to 是行号，缺省读前 50 行，to 超出文件行数时读到文件尾。",
        [
            new ToolParameter(new ToolName("path"), "相对导出目录的文件路径", Required: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
        ],
        Group);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(new FileScope(sourceDirectory, "导出目录"));

    /// <summary>把各定义绑上导出目录的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(FileScope scope) =>
    [
        ListSourceFiles.Bind(_ => List(scope)),
        ReadSourceFile.Bind(arguments => Read(scope, arguments)),
    ];

    /// <summary>导出目录内全部文件的相对路径，按序数排序，超上限截断。</summary>
    private static ErrorOr<string> List(FileScope scope)
    {
        if (!Directory.Exists(scope.Root))
        {
            return ToolErrors.Path("导出目录不存在。");
        }

        string[] files;
        try
        {
            files = [.. Directory.EnumerateFiles(scope.Root, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(scope.Root, file).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"列出导出目录失败：{ex.Message}");
        }

        if (files.Length == 0)
        {
            return "导出目录内没有文件。";
        }

        var text = new StringBuilder();
        text.AppendLine($"导出目录内文件 {files.Length} 个，路径相对导出目录：");
        foreach (string file in files.Take(MaxEntries))
        {
            text.AppendLine($"  {file}");
        }

        if (files.Length > MaxEntries)
        {
            text.Append($"…仅列出前 {MaxEntries} 条");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>读取导出目录内文件指定行号范围的内容，按行截取并按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private static ErrorOr<string> Read(FileScope scope, ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        if (string.IsNullOrWhiteSpace(path))
        {
            return ToolErrors.Argument("缺少参数 path。");
        }

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, DefaultLines);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        return scope.ReadRange(path, range.Value.From, range.Value.To, "ListSourceFiles");
    }
}
