using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>提交改动的命令定义。</summary>
internal static class Commit
{
    /// <summary>参数名：提交说明。</summary>
    private const string Message = "message";

    /// <summary>参数名：限定提交的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git commit -m {message} [-- {paths}] [:(exclude){excludePaths}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Commit"),
        "把暂存区的改动提交；给 paths 时只提交这些路径的改动，可再排除部分路径。",
        [
            new ToolParameter(new ToolName(Message), "提交说明", Required: true, AllowLeadingDash: true),
            new ToolParameter(new ToolName(Paths), "只提交这些路径的改动，缺省提交暂存区的全部改动", List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，只在给出 paths 时生效", List: true, Path: true),
        ],
        [
            new("git", null),
            new("commit", null),
            new("-m", Message),
            new("{message}", null),
            new("--", Paths),
            new("{paths}", null),
            new(":(exclude){excludePaths}", Paths),
        ]);
}