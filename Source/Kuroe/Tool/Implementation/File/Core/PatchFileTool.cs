using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

using File = System.IO.File;

/// <summary>按行号范围替换工作目录内文件内容的工具。路径解析后必须落回工作目录内，替换区间的原文须与 original 逐行一致。</summary>
public sealed class PatchFileTool : ITool
{
    private readonly FileScope _scope;

    /// <summary>按行号范围替换文件内容。</summary>
    private static readonly ToolDefinition PatchFile = new(
        new ToolPath("file/write/PatchFile"),
        "按行号范围替换工作目录内文件的内容。from 与 to 是行号，from 从 1 起；original 必须是该范围现在的原文，逐行不一致时调用被拒绝并在提示里回给实际内容；replacement 是替换后的新内容，行数可增减，为空时删除该范围；写回统一换行符，编码用 encoding 指定，缺省 utf-8 不带 BOM。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true, Path: true),
            new ToolParameter(new ToolName("from"), "起始行号，数字，第 1 行起", Required: true),
            new ToolParameter(new ToolName("to"), "结束行号，数字", Required: true),
            new ToolParameter(new ToolName("original"), "该范围现在的原文，逐行与文件比对", Required: true),
            new ToolParameter(new ToolName("replacement"), "替换后的新内容，为空时删除该范围", Required: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
        ]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public PatchFileTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [PatchFile.Bind(Patch, _scope.Paths)];
    }

    /// <summary>按行号范围替换文件内容，替换区间的原文须与 original 逐行一致；错误与拒绝以 ErrorOr 表达。行号从 1 起，replacement 行数可增减，为空时删除该范围，写回统一换行符并按 encoding 参数编码。</summary>
    private ErrorOr<string> Patch(ToolArguments arguments)
    {
        string path = arguments.Text(new ToolName("path"))!;

        ErrorOr<LineRange> range = LineRange.Parse(arguments, 0, 0);
        if (range.IsError)
        {
            return range.ErrorsOrEmptyList;
        }

        string? original = arguments.Text(new ToolName("original"));
        if (original is null)
        {
            return ToolErrors.Argument("缺少参数 original。");
        }

        if (original.Length > FileTools.ContentLimit)
        {
            return ToolErrors.Argument($"original 超过上限 {FileTools.ContentLimit} 字符。");
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

        ErrorOr<Encoding> encoding = FileTools.ParseEncoding(arguments.Text(new ToolName("encoding")));
        if (encoding.IsError)
        {
            return encoding.ErrorsOrEmptyList;
        }

        ErrorOr<string> resolved = _scope.Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (Directory.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，不能替换。");
        }

        if (!File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        string[] lines;
        try
        {
            lines = [.. File.ReadLines(resolved.Value)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 失败：{ex.Message}");
        }

        if (range.Value.From > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，起始行 {range.Value.From} 超出。");
        }

        if (range.Value.To > lines.Length)
        {
            return ToolErrors.Argument($"{path} 共 {lines.Length} 行，结束行 {range.Value.To} 超出。");
        }

        string[] current = lines[(range.Value.From - 1)..range.Value.To];
        if (!current.SequenceEqual(SplitLines(original)))
        {
            return ToolErrors.Argument($"第 {range.Value.From} 到 {range.Value.To} 行的原文与 original 不一致，实际内容是：\n{FileScope.Truncate(string.Join('\n', current))}");
        }

        string[] replacementLines = SplitLines(replacement);
        string[] newLines = [.. lines[..(range.Value.From - 1)], .. replacementLines, .. lines[range.Value.To..]];
        try
        {
            File.WriteAllText(resolved.Value, string.Join("\n", newLines), encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已替换 {path} 的第 {range.Value.From} 到 {range.Value.To} 行（{current.Length} 行 → {replacementLines.Length} 行）。";
    }

    /// <summary>按行拆文本并去行尾回车的行数组，空文本得空数组。</summary>
    private static string[] SplitLines(string text) => text.Length == 0
        ? []
        : [.. text.Split('\n').Select(line => line.TrimEnd('\r'))];
}