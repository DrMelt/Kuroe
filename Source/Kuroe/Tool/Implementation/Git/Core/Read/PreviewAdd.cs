using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>预演暂存的命令定义。</summary>
internal static class PreviewAdd
{
    /// <summary>参数名：要预演暂存的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git add --dry-run -- {paths} [:(exclude){excludePaths}]。dry-run 固定在模板里，没有关闭开关。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/PreviewAdd"),
        "预演把指定路径的改动放入暂存区，列出将暂存的路径，不改动暂存区。",
        [
            new ToolParameter(new ToolName(Paths), "要预演暂存的文件或目录路径列表", Required: true, List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true),
        ],
        [
            new("git", null),
            new("add", null),
            new("--dry-run", null),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ]);
}