using System.Text;
using ErrorOr;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

using File = System.IO.File;

/// <summary>把文本覆盖写入工作目录内文件的工具。路径解析后必须落回工作目录内，父目录缺失时自动创建。</summary>
public sealed class WriteFileTool : ITool
{
    private readonly FileScope _scope;

    /// <summary>覆盖写入文件。</summary>
    private static readonly ToolDefinition WriteFile = new(
        new ToolPath("file/write/WriteFile"),
        "把文本覆盖写入工作目录内的文件，文件不存在时创建，已存在时直接覆盖其内容。content 上限 40000 字符，父目录缺失时自动创建；encoding 指定写回编码，缺省 utf-8 不带 BOM。",
        [
            new ToolParameter(new ToolName("path"), "相对工作目录的文件路径", Required: true, Path: true),
            new ToolParameter(new ToolName("content"), "要写入文件的文本", Required: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8"),
        ]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public WriteFileTool(string baseDirectory)
    {
        _scope = FileTools.Scope(baseDirectory);
        Functions = [WriteFile.Bind(Write, _scope.Paths)];
    }

    /// <summary>把文本覆盖写入工作目录内的文件，父目录缺失时自动创建，已有文件直接覆盖；错误与拒绝以 ErrorOr 表达。编码按 encoding 参数，缺省 UTF-8 无 BOM。</summary>
    private ErrorOr<string> Write(ToolArguments arguments)
    {
        string path = arguments.Text(new ToolName("path"))!;

        string? content = arguments.Text(new ToolName("content"));
        if (content is null)
        {
            return ToolErrors.Argument("缺少参数 content。");
        }

        if (content.Length > FileTools.ContentLimit)
        {
            return ToolErrors.Argument($"content 超过上限 {FileTools.ContentLimit} 字符，本次写入不执行。");
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
            return ToolErrors.Path($"{path} 是目录，不能写入。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolved.Value);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(resolved.Value, content, encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已写入 {content.Length} 字符到 {path}。";
    }
}