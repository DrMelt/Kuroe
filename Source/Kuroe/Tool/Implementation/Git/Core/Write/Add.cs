using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>把改动放入暂存区的命令定义。</summary>
internal static class Add
{
    /// <summary>参数名：要暂存的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git add -- {paths} [:(exclude){excludePaths}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Add"),
        "把指定路径的改动放入暂存区，供 Commit 提交；可排除部分路径。",
        [
            new ToolParameter(new ToolName(Paths), "要暂存的文件或目录路径列表", Required: true, List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true),
        ],
        [
            new("git", null),
            new("add", null),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ]);
}