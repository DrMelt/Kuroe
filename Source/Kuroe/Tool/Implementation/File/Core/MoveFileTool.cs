using ErrorOr;
using Kuroe.Shared;
using Kuroe.Tool.FileAccess;

namespace Kuroe.Tool.File;

using File = System.IO.File;

/// <summary>移动路径基准目录内文件或目录的工具。路径解析后必须落回工作区内，目标已存在时拒绝。</summary>
public sealed class MoveFileTool : ITool
{
    private readonly FileScope _scope;

    /// <summary>把文件或目录移动到另一个路径。</summary>
    public static ToolDefinition Definition { get; } = new(
        new ToolPath("file/write/MoveFile"),
        "把路径基准目录内的文件或目录移动到另一个路径。source 是现有的文件或目录，target 是移动后的完整路径，移动到同一目录下的新名字即重命名。目标已存在时拒绝，目标父目录缺失时自动创建。",
        [
            new ToolParameter(new ToolName("source"), "相对路径基准目录的现有文件或目录路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
            new ToolParameter(new ToolName("target"), "相对路径基准目录的移动后完整路径", Required: true, Path: true, PathAccess: FileTools.PathAccess),
        ], [PathVariables.RootName]);

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>用工作目录建立载体。</summary>
    public MoveFileTool(string baseDirectory)
        : this(ToolBinding.Root(baseDirectory))
    {
    }

    /// <summary>用执行绑定建立载体，路径变量由工具自行读取。</summary>
    public MoveFileTool(ToolBinding binding)
    {
        _scope = FileTools.Scope(binding);
        Functions = [Definition.Bind(Move, _scope.Paths)];
    }

    /// <summary>把文件或目录移动到工作目录内的另一个路径，目标已存在时拒绝，父目录缺失时自动创建；错误与拒绝以 ErrorOr 表达。</summary>
    private ErrorOr<string> Move(ToolArguments arguments)
    {
        string source = arguments.Text(new ToolName("source"))!;
        string target = arguments.Text(new ToolName("target"))!;

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

        bool isDirectory = Directory.Exists(resolvedSource.Value);
        if (!isDirectory && !File.Exists(resolvedSource.Value))
        {
            return ToolErrors.Path($"{source} 不存在。");
        }

        if (File.Exists(resolvedTarget.Value) || Directory.Exists(resolvedTarget.Value))
        {
            return ToolErrors.Path($"{target} 已存在，不能覆盖。");
        }

        if (isDirectory && IsWithin(resolvedTarget.Value, resolvedSource.Value))
        {
            return ToolErrors.Path($"{target} 在 {source} 之内，不能移动到自身子目录。");
        }

        try
        {
            string? directory = Path.GetDirectoryName(resolvedTarget.Value);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (isDirectory)
            {
                Directory.Move(resolvedSource.Value, resolvedTarget.Value);
            }
            else
            {
                File.Move(resolvedSource.Value, resolvedTarget.Value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ToolErrors.WriteFailed($"移动 {source} 到 {target} 失败：{ex.Message}");
        }

        return $"已把 {source} 移动到 {target}。";
    }

    /// <summary>目标路径是否落在源目录之内，按平台的文件系统大小写规则比较。</summary>
    private static bool IsWithin(string path, string directory) =>
        path.Length > directory.Length
        && path.StartsWith(
            string.Concat(directory, Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}