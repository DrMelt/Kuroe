using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Output;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File;

/// <summary>列出路径基准目录内目录条目的工具。路径解析后必须落回工作区内。</summary>
public sealed class ListDirectoryTool : ITool
{
    private readonly IFileScope _scope;

    /// <summary>列出目录的直接子项。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/read/ListDirectory"),
        "列出路径基准目录内一个目录的直接子项。",
        [new ToolParameter(new ToolName("path"), "相对路径基准目录的目录路径，缺省为路径基准目录", Path: true, PathAccess: FileTools.PathAccess)],
        [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public ListDirectoryTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public ListDirectoryTool(ToolBinding binding, IWorkspaceFiles files)
        : this(FileTools.Scope(binding, files))
    {
    }

    /// <summary>用已建立的文件访问作用域建立载体。</summary>
    public ListDirectoryTool(IFileScope scope)
    {
        _scope = scope;
        Functions = [Definition.Bind(List, scope.Paths)];
    }

    /// <summary>列出目录的直接子项，按名字排序并标注目录；错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> List(ToolArguments arguments)
    {
        string? path = arguments.Text(new ToolName("path"));
        path ??= string.Empty;

        ErrorOr<IReadOnlyList<DirectoryEntry>> entries = _scope.ListEntries(path, "ReadFile");
        if (entries.IsError)
        {
            return entries.ErrorsOrEmptyList;
        }

        return new DirectoryListingText(_scope.Display(path), entries.Value).ToString();
    }
}