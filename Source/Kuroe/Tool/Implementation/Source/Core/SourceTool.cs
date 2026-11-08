using ErrorOr;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.Source;

/// <summary>导出资料的查阅工具：按目录列出程序随附的源码与文档路径，按行号范围读取原文。
/// 只读，路径解析后必须落回导出目录内。</summary>
/// <param name="sourceDirectory">随程序导出的源码与文档目录。</param>
/// <param name="files">文件访问实现。</param>
public sealed class SourceTool(string sourceDirectory, IWorkspaceFiles files) : ITool
{
    /// <summary>ReadSourceFile 缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    /// <summary>列出导出目录内一个目录的直接子项。</summary>
    private static readonly ToolDefinition ListSourceDirectory = new(
        new ToolPath("source/ListSourceDirectory"),
        "列出导出目录内一个目录的直接子项。",
        [new ToolParameter(new ToolName("path"), "相对导出目录的目录路径，缺省为导出目录根", Path: true)]);

    /// <summary>读取导出目录内文件指定行号范围。</summary>
    private static readonly ToolDefinition ReadSourceFile = new(
        new ToolPath("source/ReadSourceFile"),
        $"读取导出目录内源码或文档指定行号范围的内容，行尾保留文件原有的换行符；以终止符结尾的文件在末尾还有一个空行。输出文本以 {MarkedText.Begin} 与 {MarkedText.End} 包住，结束标记不额外添加换行；文件没有内容时只返回提示，不带范围标记。起始行超出文件行数时拒绝，到达文件尾时在正文前注明实际范围。",
        [
            new ToolParameter(new ToolName("path"), "相对导出目录的文件路径", Required: true, Path: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
            new ToolParameter(new ToolName("lineNumbers"), "是否在每行前输出行号，布尔，缺省为真；为假时只输出行内容与原始换行符", Flag: true),
        ]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; } = Declare(new FileScope(sourceDirectory, "导出目录", files));

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

        return new DirectoryListingText(scope.Display(path), entries.Value).ToString();
    }

    /// <summary>读取导出目录内文件指定行号范围的内容，缺省每行带行号并保留行尾终止符，按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private static ErrorOr<string> Read(FileScope scope, ToolArguments arguments)
    {
        string path = arguments.RequiredText(new ToolName("path"));

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, DefaultLines);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        bool lineNumbers = arguments.Flag(new ToolName("lineNumbers")) ?? true;
        ErrorOr<SourceLineReadResult> read = scope.ReadRange(
            path,
            range.Value.From,
            range.Value.To,
            TextOutputLimit.Characters,
            "ListSourceDirectory");
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        return new ReadRangeText(read.Value, lineNumbers).ToString();
    }
}
