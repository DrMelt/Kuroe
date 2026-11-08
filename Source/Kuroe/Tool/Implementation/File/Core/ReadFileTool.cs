using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>读取路径基准目录内文本文件指定行号范围的工具。路径解析后必须落回工作区内。</summary>
public sealed class ReadFileTool : ITool
{
    /// <summary>缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    private readonly FileScope _scope;

    /// <summary>读取单个文件指定行号范围。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/ReadFile"),
        $"读取路径基准目录内文本文件指定行号范围的内容，缺省每行带行号，行尾保留文件原有的换行符。lineNumbers 为假时只输出行内容与原始换行符；以终止符结尾的文件在末尾还有一个空行。输出文本以 {TextRange.Begin} 与 {TextRange.End} 包住，结束标记不额外添加换行；文件没有内容时只返回提示，不带范围标记。from 与 to 是行号，缺省读前 50 行，from 超出文件行数时拒绝，to 超出文件行数时读到文件尾并在正文前注明实际范围。",
        [
            new ToolParameter(new ToolName("path"), "相对路径基准目录的文件路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
            new ToolParameter(new ToolName("lineNumbers"), "是否在每行前输出行号，布尔，缺省为真；为假时只输出行内容与原始换行符", Flag: true),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public ReadFileTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public ReadFileTool(ToolBinding binding, IWorkspaceFiles files)
    {
        _scope = FileTools.Scope(binding, files);
        Functions = [Definition.Bind(Read, _scope.Paths)];
    }

    /// <summary>读取指定文本文件指定行号范围的内容，缺省每行带行号并保留行尾终止符，按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private ErrorOr<string> Read(ToolArguments arguments)
    {
        string path = arguments.RequiredText(new ToolName("path"));

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, DefaultLines);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        bool lineNumbers = arguments.Flag(new ToolName("lineNumbers")) ?? true;
        return _scope.ReadRange(path, range.Value.From, range.Value.To, lineNumbers, "ListDirectory");
    }
}