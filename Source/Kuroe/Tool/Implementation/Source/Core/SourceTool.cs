using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.Source;

/// <summary>导出资料的查阅工具：按目录列出程序随附的源码与文档路径，按行号范围读取原文。
/// 只读，路径解析后必须落回导出目录内。</summary>
public sealed class SourceTool(string sourceDirectory) : ITool
{
    /// <summary>ReadSourceFile 缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    /// <summary>目录列表的条目上限，超出截断并提示。</summary>
    private const int MaxEntries = 200;

    /// <summary>列出导出目录内一个目录的直接子项。</summary>
    private static readonly ToolDefinition ListSourceDirectory = new(
        new ToolPath("source/ListSourceDirectory"),
        "列出导出目录内一个目录的直接子项。path 缺省时列出导出目录根的条目。",
        [new ToolParameter(new ToolName("path"), "相对导出目录的目录路径，缺省为导出目录根", Path: true)]);

    /// <summary>读取导出目录内文件指定行号范围。</summary>
    private static readonly ToolDefinition ReadSourceFile = new(
        new ToolPath("source/ReadSourceFile"),
        "读取导出目录内源码或文档指定行号范围的内容。from 与 to 是行号，缺省读前 50 行，to 超出文件行数时读到文件尾。",
        [
            new ToolParameter(new ToolName("path"), "相对导出目录的文件路径", Required: true, Path: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
        ]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(new FileScope(sourceDirectory, "导出目录"));

    /// <summary>把各定义绑上导出目录的调用体。</summary>
    private static IReadOnlyList<ToolFunction> Declare(FileScope scope) =>
    [
        ListSourceDirectory.Bind(arguments => List(scope, arguments), scope.Paths),
        ReadSourceFile.Bind(arguments => Read(scope, arguments), scope.Paths),
    ];

    /// <summary>导出目录内一个目录的直接子项，按名字序数排序并标注目录，超上限截断。</summary>
    private static ErrorOr<string> List(FileScope scope, ToolArguments arguments)
    {
        string path = arguments.Text(new ToolName("path")) ?? string.Empty;

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = scope.ListEntries(path, "ReadSourceFile");
        if (entries.IsError)
        {
            return entries.ErrorsOrEmptyList;
        }

        if (entries.Value.Count == 0)
        {
            return $"目录 {scope.Display(path)} 为空。";
        }

        var text = new StringBuilder();
        text.AppendLine($"{scope.Display(path)} 的条目（{entries.Value.Count}）：");
        foreach (DirectoryEntry entry in entries.Value.Take(MaxEntries))
        {
            text.AppendLine($"  {entry.Name}{(entry.IsDirectory ? "/" : string.Empty)}");
        }

        if (entries.Value.Count > MaxEntries)
        {
            text.Append($"…仅列出前 {MaxEntries} 条");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>读取导出目录内文件指定行号范围的内容，按行截取并按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private static ErrorOr<string> Read(FileScope scope, ToolArguments arguments)
    {
        string path = arguments.Text(new ToolName("path"))!;

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, DefaultLines);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        return scope.ReadRange(path, range.Value.From, range.Value.To, "ListSourceDirectory");
    }
}
