using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;
using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;

namespace Kuroe.Tool.File;

/// <summary>在路径基准目录内多个文本文件中按字面文本或正则表达式批量替换的工具。路径解析后必须落回工作区内，逐文件保留编码、BOM 与换行风格。</summary>
public sealed class ReplaceTextTool : ITool
{
    private readonly IFileScope _scope;
    private readonly IFileReplacer _replacer;

    /// <summary>在多个文本文件中按字面文本或正则表达式批量替换。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/ReplaceText"),
        "在指定目录或文件内的文本文件中，按字面文本或正则表达式批量替换。每个文件替换全部不重叠匹配，正则模式的替换文本支持 $1 与 ${name} 分组引用，正则作用于归一换行后的整段文件内容，保留原文件的编码、BOM 与换行风格，跳过二进制文件。",
        [
            new ToolParameter(new ToolName("paths"), "相对路径基准目录的目录或文件路径列表，目录递归展开", Required: true, List: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("pattern"), "要查找的文本，regex 为假时按字面文本解释，为真时按 .NET 正则解释，空拒绝", Required: true),
            new ToolParameter(new ToolName("replacement"), "替换后的文本，可为空用于删除；regex 为真时按 .NET 替换模板解释分组引用", Required: true),
            new ToolParameter(new ToolName("extensions"), "限定文件后缀名，例如 cs 或 .cs，省略时不限制", List: true),
            new ToolParameter(new ToolName("regex"), "pattern 是否按正则表达式解释，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName("dryRun"), "只报告将发生的替换，不写入文件，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省保留各文件编码；给出时所有写回文件使用该编码"),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public ReplaceTextTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public ReplaceTextTool(ToolBinding binding, IWorkspaceFiles files)
        : this(FileTools.Scope(binding, files))
    {
    }

    /// <summary>用已建立的文件访问作用域建立载体，替换使用该作用域内的批量文本替换。</summary>
    private ReplaceTextTool(FileScope scope)
        : this(scope, new FileReplacer(scope, FileTools.PathAccess))
    {
    }

    /// <summary>用已建立的文件访问作用域与批量文本替换建立载体。</summary>
    public ReplaceTextTool(IFileScope scope, IFileReplacer replacer)
    {
        _scope = scope;
        _replacer = replacer;
        Functions = [Definition.Bind(Replace, scope.Paths)];
    }

    /// <summary>按路径范围批量替换文本，错误与拒绝以 ErrorOr 表达。单个文件读写失败写入结果文本，不中断整批。</summary>
    private ErrorOr<string> Replace(ToolArguments arguments)
    {
        string? pattern = arguments.Text(new ToolName("pattern"));
        if (pattern is null)
        {
            return ToolErrors.Argument("缺少参数 pattern。");
        }

        if (pattern.Length > FileTools.ContentLimit)
        {
            return ToolErrors.Argument($"pattern 超过上限 {FileTools.ContentLimit} 字符。");
        }

        string? replacement = arguments.Text(new ToolName("replacement"));
        if (replacement is null)
        {
            return ToolErrors.Argument("缺少参数 replacement。");
        }

        if (replacement.Length > FileTools.ContentLimit)
        {
            return ToolErrors.Argument($"replacement 超过上限 {FileTools.ContentLimit} 字符。");
        }

        ErrorOr<TextQuery> query = TextQuery.Create(
            pattern,
            arguments.List(new ToolName("extensions")),
            arguments.Flag(new ToolName("regex")) == true);
        if (query.IsError)
        {
            return query.ErrorsOrEmptyList;
        }

        ErrorOr<TextReplacement> text = TextReplacement.Create(query.Value, replacement);
        if (text.IsError)
        {
            return text.ErrorsOrEmptyList;
        }

        string? encodingName = arguments.Text(new ToolName("encoding"));
        Encoding? encodingOverride = null;
        if (!string.IsNullOrWhiteSpace(encodingName))
        {
            ErrorOr<Encoding> encoding = FileTools.ParseEncoding(encodingName);
            if (encoding.IsError)
            {
                return encoding.ErrorsOrEmptyList;
            }

            encodingOverride = encoding.Value;
        }

        IReadOnlyList<string> paths = arguments.List(new ToolName("paths")) ?? [];
        if (paths.Count == 0)
        {
            return ToolErrors.Argument("缺少参数 paths。");
        }

        bool dryRun = arguments.Flag(new ToolName("dryRun")) == true;

        ErrorOr<TextReplaceResult> result = _replacer.Replace(paths, text.Value, dryRun, encodingOverride);
        if (result.IsError)
        {
            return result.ErrorsOrEmptyList;
        }

        return new ReplaceReportText(result.Value, paths, dryRun, _scope.Root).ToString();
    }
}