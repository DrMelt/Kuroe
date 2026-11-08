using ErrorOr;
using Kuroe.Shared;
using Kuroe.Files.Shared;
using Kuroe.Tool.FileAccess.Shared;

namespace Kuroe.Tool.File;

/// <summary>移动路径基准目录内文件或目录的工具。路径解析后必须落回工作区内，目标已存在时拒绝。</summary>
public sealed class MoveFileTool : ITool
{
    private readonly IFileScope _scope;
    private readonly IWorkspaceFiles _files;

    /// <summary>把文件或目录移动到另一个路径。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/MoveFile"),
        "把路径基准目录内的文件或目录移动到另一个路径，移动到同一目录下的新名字即重命名。目标已存在时拒绝，目标父目录缺失时自动创建。",
        [
            new ToolParameter(new ToolName("source"), "相对路径基准目录的现有文件或目录路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("target"), "相对路径基准目录的移动后完整路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录与文件访问实现建立载体。</summary>
    public MoveFileTool(string baseDirectory, IWorkspaceFiles files)
        : this(ToolBinding.Root(baseDirectory), files)
    {
    }

    /// <summary>用执行绑定与文件访问实现建立载体，路径变量由工具自行读取。</summary>
    public MoveFileTool(ToolBinding binding, IWorkspaceFiles files)
        : this(FileTools.Scope(binding, files), files)
    {
    }

    /// <summary>用已建立的文件访问作用域与文件访问实现建立载体。</summary>
    public MoveFileTool(IFileScope scope, IWorkspaceFiles files)
    {
        _scope = scope;
        _files = files;
        Functions = [Definition.Bind(Move, scope.Paths)];
    }

    /// <summary>把文件或目录移动到工作目录内的另一个路径，目标已存在时拒绝，父目录缺失时自动创建；错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> Move(ToolArguments arguments)
    {
        string source = arguments.RequiredText(new ToolName("source"));
        string target = arguments.RequiredText(new ToolName("target"));

        ErrorOr<string> resolvedSource = _scope.Resolve(source);
        if (resolvedSource.IsError)
        {
            return resolvedSource.ErrorsOrEmptyList;
        }

        ErrorOr<string> resolvedTarget = _scope.Resolve(target);
        if (resolvedTarget.IsError)
        {
            return resolvedTarget.ErrorsOrEmptyList;
        }

        bool isDirectory = _files.DirectoryExists(resolvedSource.Value);
        if (!isDirectory && !_files.FileExists(resolvedSource.Value))
        {
            return ToolErrors.Path($"{source} 不存在。");
        }

        if (_files.FileExists(resolvedTarget.Value) || _files.DirectoryExists(resolvedTarget.Value))
        {
            return ToolErrors.Path($"{target} 已存在，不能覆盖。");
        }

        if (isDirectory && WorkspacePath.IsWithin(resolvedSource.Value, resolvedTarget.Value))
        {
            return ToolErrors.Path($"{target} 在 {source} 之内，不能移动到自身子目录。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolvedTarget.Value);
            if (!string.IsNullOrEmpty(directory))
            {
                _files.CreateDirectory(directory);
            }

            if (isDirectory)
            {
                _files.MoveDirectory(resolvedSource.Value, resolvedTarget.Value);
            }
            else
            {
                _files.MoveFile(resolvedSource.Value, resolvedTarget.Value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"移动 {source} 到 {target} 失败：{ex.Message}");
        }

        return $"已把 {source} 移动到 {target}。";
    }
}