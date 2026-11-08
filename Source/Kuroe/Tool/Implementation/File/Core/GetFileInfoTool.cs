using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Shared;
using Kuroe.Tool.File.Output;

namespace Kuroe.Tool.File;

/// <summary>取路径基准目录内文件的文件系统信息的工具。路径解析后必须落回工作区内，只受理文件。</summary>
public sealed class GetFileInfoTool : ITool
{
    private readonly IFileScope _scope;
    private readonly IWorkspaceFiles _files;

    /// <summary>取一个文件的文件系统信息。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/GetFileInfo"),
        "取路径基准目录内一个文件的文件系统信息：字节大小、创建/修改/访问时间、只读隐藏系统归档属性。只报文件系统事实，不判断内容；目录用 ListDirectory 列出条目。",
        [new ToolParameter(new ToolName("path"), "相对路径基准目录的文件路径", Required: true, Path: true, PathAccess: FileTools.PathAccess)],
        [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public GetFileInfoTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public GetFileInfoTool(ToolBinding binding, IWorkspaceFiles files)
        : this(FileTools.Scope(binding, files), files)
    {
    }

    /// <summary>用已建立的文件访问作用域与文件访问实现建立载体。</summary>
    public GetFileInfoTool(IFileScope scope, IWorkspaceFiles files)
    {
        _scope = scope;
        _files = files;
        Functions = [Definition.Bind(Info, scope.Paths)];
    }

    /// <summary>取一个文件的文件系统事实并渲染成文本，错误与拒绝以 ErrorOr 表达。目录不受理，提示改用 ListDirectory。</summary>
    private ErrorOr<string> Info(ToolArguments arguments)
    {
        string path = arguments.RequiredText(new ToolName("path"));

        ErrorOr<string> file = _scope.ResolveFile(path, "ListDirectory");
        if (file.IsError)
        {
            return file.ErrorsOrEmptyList;
        }

        try
        {
            return new FileInfoText(path, _files.Inspect(file.Value)).ToString();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.ReadFailed($"读取 {path} 信息失败：{ex.Message}");
        }
    }
}