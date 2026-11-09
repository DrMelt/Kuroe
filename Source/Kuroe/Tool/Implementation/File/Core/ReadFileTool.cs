using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File;

/// <summary>读取路径基准目录内文本文件指定行号范围的工具。路径解析后必须落回工作区内。</summary>
public sealed class ReadFileTool : ITool
{
    private readonly IFileScope _scope;

    /// <summary>读取单个文件指定行号范围。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/ReadFile"),
        $"读取路径基准目录内文本文件指定行号范围的内容，行尾保留文件原有的换行符；以终止符结尾的文件在末尾还有一个空行。正文总字符缺省 {ReadLimits.DefaultCharacters}，最多 {ReadLimits.MaximumCharacters}；单行行长缺省 {ReadLimits.DefaultLineCharacters} 字符，超出时截断并丢弃终止符。输出文本以 {MarkedText.Begin} 与 {MarkedText.End} 包住，结束标记不额外添加换行；文件没有内容时只返回提示，不带范围标记。起始行超出文件行数时拒绝，到达文件尾时在正文前注明实际范围。",
        [
            new ToolParameter(new ToolName("path"), "相对路径基准目录的文件路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省读到文件尾"),
            new ToolParameter(new ToolName("lineNumbers"), "是否在每行前输出行号，布尔，缺省为真；为假时只输出行内容与原始换行符", Flag: true),
            new ToolParameter(new ToolName("maxChars"), $"正文总字符上限，数字，缺省 {ReadLimits.DefaultCharacters}，最大 {ReadLimits.MaximumCharacters}"),
            new ToolParameter(new ToolName("maxLineChars"), $"单行行长上限，含终止符，数字，缺省 {ReadLimits.DefaultLineCharacters}"),
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
        : this(FileTools.Scope(binding, files))
    {
    }

    /// <summary>用已建立的文件访问作用域建立载体。</summary>
    public ReadFileTool(IFileScope scope)
    {
        _scope = scope;
        Functions = [Definition.Bind(Read, scope.Paths)];
    }

    /// <summary>读取指定文本文件指定行号范围的内容，缺省每行带行号并保留行尾终止符，按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行省略或超出文件长度时读到文件尾。</summary>
    private ErrorOr<string> Read(ToolArguments arguments)
    {
        string path = arguments.RequiredText(new ToolName("path"));

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 1, null);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        ErrorOr<ReadLimits> limits = ReadLimits.Parse(arguments);
        if (limits.IsError)
        {
            return limits.ErrorsOrEmptyList;
        }

        bool lineNumbers = arguments.Flag(new ToolName("lineNumbers")) ?? true;
        ErrorOr<SourceLineReadResult> read = _scope.ReadRange(
            path,
            range.Value.From,
            range.Value.To,
            limits.Value.Characters,
            limits.Value.LineCharacters,
            "ListDirectory");
        if (read.IsError)
        {
            return read.ErrorsOrEmptyList;
        }

        return new ReadRangeText(read.Value, lineNumbers, limits.Value.Characters).ToString();
    }
}