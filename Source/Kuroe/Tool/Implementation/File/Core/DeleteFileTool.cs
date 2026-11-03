using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

using File = System.IO.File;

/// <summary>删除路径基准目录内文件或目录的工具。路径解析后必须落回工作区内，非空目录需要 recursive，路径基准目录本身拒绝删除。</summary>
public sealed class DeleteFileTool : ITool
{
    private readonly FileScope _scope;

    /// <summary>删除文件或目录。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/DeleteFile"),
        "删除路径基准目录内的文件或目录。文件直接删除；目录在 recursive 为真时连同内容一起删除，缺省时只删除空目录。路径基准目录本身不能删除，目标不存在时拒绝。",
        [
            new ToolParameter(new ToolName("path"), "相对路径基准目录的现有文件或目录路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("recursive"), "删除目录时是否连同其内容一起删除，布尔，缺省为假", Flag: true),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public DeleteFileTool(string baseDirectory)
        : this(ToolBinding.Root(baseDirectory))
    {
    }

    /// <summary>用执行绑定建立载体，路径变量由工具自行读取。</summary>
    public DeleteFileTool(ToolBinding binding)
    {
        _scope = FileTools.Scope(binding);
        Functions = [Definition.Bind(Delete, _scope.Paths)];
    }

    /// <summary>删除工作目录内的文件或目录；错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> Delete(ToolArguments arguments)
    {
        string path = arguments.Text(new ToolName("path"))!;
        bool recursive = arguments.Flag(new ToolName("recursive")) == true;

        ErrorOr<string> resolved = _scope.Resolve(path);
        if (resolved.IsError)
        {
            return resolved.ErrorsOrEmptyList;
        }

        if (WorkspacePath.IsSame(resolved.Value, _scope.Root))
        {
            return ToolErrors.Path($"{path} 是路径基准目录本身，不能删除。");
        }

        bool isDirectory = Directory.Exists(resolved.Value);
        if (!isDirectory && !File.Exists(resolved.Value))
        {
            return ToolErrors.Path($"{path} 不存在。");
        }

        try
        {
            if (!isDirectory)
            {
                File.Delete(resolved.Value);
            }
            else if (!recursive && Directory.EnumerateFileSystemEntries(resolved.Value).Any())
            {
                return ToolErrors.Path($"目录 {path} 不为空，recursive 为真时连同内容一起删除。");
            }
            else
            {
                Directory.Delete(resolved.Value, recursive);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"删除 {path} 失败：{ex.Message}");
        }

        return isDirectory ? $"已删除目录 {path}。" : $"已删除文件 {path}。";
    }
}