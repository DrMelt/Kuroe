using System.Text;
using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File;

/// <summary>把文本覆盖写入路径基准目录内文件的工具。路径解析后必须落回工作区内，父目录缺失时自动创建。</summary>
public sealed class WriteFileTool : ITool
{
    private readonly IFileScope _scope;
    private readonly IWorkspaceFiles _files;

    /// <summary>覆盖写入文件。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/WriteFile"),
        "把文本覆盖写入路径基准目录内的文件，文件不存在时创建，已存在时直接覆盖其内容；父目录缺失时自动创建。",
        [
            new ToolParameter(new ToolName("path"), "相对路径基准目录的文件路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("content"), $"要写入文件的文本，上限 {FileTools.ContentLimit} 字符", Required: true),
            new ToolParameter(new ToolName("encoding"), "写回编码，支持 utf-8、utf-8-bom、utf-16、utf-16be，缺省 utf-8 不带 BOM"),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public WriteFileTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public WriteFileTool(ToolBinding binding, IWorkspaceFiles files)
        : this(FileTools.Scope(binding, files), files)
    {
    }

    /// <summary>用已建立的文件访问作用域与文件访问实现建立载体。</summary>
    public WriteFileTool(IFileScope scope, IWorkspaceFiles files)
    {
        _scope = scope;
        _files = files;
        Functions = [Definition.Bind(Write, scope.Paths)];
    }

    /// <summary>把文本覆盖写入工作目录内的文件，父目录缺失时自动创建，已有文件直接覆盖；错误与拒绝以 ErrorOr 表达。编码按 encoding 参数，缺省 UTF-8 无 BOM。</summary>
    private ErrorOr<string> Write(ToolArguments arguments)
    {
        string path = arguments.RequiredText(new ToolName("path"));

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

        if (_files.DirectoryExists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 是目录，不能写入。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolved.Value);
            if (!string.IsNullOrEmpty(directory))
            {
                _files.CreateDirectory(directory);
            }

            _files.WriteText(resolved.Value, content, encoding.Value);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"写入 {path} 失败：{ex.Message}");
        }

        return $"已写入 {content.Length} 字符到 {path}。";
    }
}