using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

/// <summary>在路径基准目录内多个文本文件中按字面文本批量替换的工具。路径解析后必须落回工作区内，逐文件保留编码、BOM 与换行风格。</summary>
public sealed class ReplaceTextTool : ITool
{
    private readonly FileScope _scope;
    private readonly FileReplacer _replacer;

    /// <summary>在多个文本文件中按字面文本批量替换。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/ReplaceText"),
        "在指定目录或文件内的文本文件中，按字面文本批量替换。每个文件替换全部不重叠匹配，保留原文件的编码、BOM 与换行风格，跳过二进制文件。",
        [
            new ToolParameter(new ToolName("paths"), "相对路径基准目录的目录或文件路径列表，目录递归展开", Required: true, List: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("pattern"), "要查找的字面文本，不按正则解释", Required: true),
            new ToolParameter(new ToolName("replacement"), "替换后的文本，可为空用于删除", Required: true),
            new ToolParameter(new ToolName("extensions"), "限定文件后缀名，例如 cs 或 .cs，省略时不限制", List: true),
            new ToolParameter(new ToolName("ignoreCase"), "匹配是否忽略大小写，布尔，缺省为假", Flag: true),
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
    {
        _scope = FileTools.Scope(binding, files);
        _replacer = new FileReplacer(_scope, FileTools.PathAccess);
        Functions = [Definition.Bind(Replace, _scope.Paths)];
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
            arguments.Flag(new ToolName("ignoreCase")) == true);
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

        return Render(result.Value, paths, dryRun);
    }

    /// <summary>把批处理结果渲染成总数、失败项与逐文件明细，整体超过输出上限时截断并提示。</summary>
    private string Render(TextReplaceResult result, IReadOnlyList<string> paths, bool dryRun)
    {
        string scope = string.Join("、", paths);
        if (result.Files.Count == 0)
        {
            return $"在 {scope} 中扫描 {result.ScannedFiles} 个文件，没有匹配，未修改文件。";
        }

        string action = dryRun ? "将替换" : "已替换";
        var text = new StringBuilder();
        string summary = $"在 {scope} 中扫描 {result.ScannedFiles} 个文件";
        text.Append($"{summary}，{action} {result.ChangedFiles} 个文件的 {result.Replacements} 处文本。");

        TextReplaceFile[] changed = [.. result.Files.Where(file => file.Error is null)];
        TextReplaceFile[] failed = [.. result.Files.Where(file => file.Error is not null)];

        if (failed.Length > 0)
        {
            text.Append($"\n失败 {failed.Length} 个文件：");
            foreach (TextReplaceFile file in failed)
            {
                text.Append($"\n{DisplayPath(file.File)}：{file.Error}");
            }
        }

        if (changed.Length > 0)
        {
            text.Append(dryRun ? "\n将修改：" : "\n已修改：");
            foreach (TextReplaceFile file in changed)
            {
                text.Append($"\n{DisplayPath(file.File)}：{file.Replacements} 处，第 {string.Join("、", file.Lines)} 行");
            }
        }

        return TextLimit.WithNotice(text.ToString(), out _);
    }

    /// <summary>文件的展示路径，相对路径基准目录。</summary>
    private string DisplayPath(string file) => Path.GetRelativePath(_scope.Root, file);
}