using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>读取工作目录内文本文件指定行号范围的工具。路径解析后必须落回工作目录内。</summary>
public sealed class ReadFileTool : ITool
{
    /// <summary>缺省读取的行数，不传范围时只读文件开头这些行。</summary>
    private const int DefaultLines = 50;

    private readonly FileScope _scope;

    /// <summary>读取单个文件指定行号范围。</summary>
    private static readonly ToolDefinition ReadFile = new(
        new ToolPath("file/read/ReadFile"),
        "读取工作目录内文本文件指定行号范围的内容。from 与 to 是行号，缺省读前 50 行，to 超出文件行数时读到文件尾。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，缺省 1"),
            new ToolParameter(new ToolName("to"), "结束行号，数字，缺省 50，超出文件行数时读到文件尾"),
        ]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public ReadFileTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [ReadFile.Bind(Read)];
    }

    /// <summary>读取指定文本文件指定行号范围的内容，按行截取并按字符上限截断；错误与拒绝以 ErrorOr 表达。行号从 1 起，结束行超出文件长度时读到文件尾。</summary>
    private ErrorOr<string> Read(ToolArguments arguments)
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

        return _scope.ReadRange(path, range.Value.From, range.Value.To, "ListDirectory");
    }
}