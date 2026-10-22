using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>提交改动的命令定义。</summary>
internal static class Commit
{
    /// <summary>参数名：提交说明。</summary>
    private const string Message = "message";

    /// <summary>参数名：限定提交的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>函数定义与模板：git commit -m {message} [-- paths]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Commit"),
        "把暂存区的改动提交，message 是提交说明。给 paths 时只提交这些路径的改动。",
        [
            new CommandToolParameter(Message, "提交说明", Required: true),
            new CommandToolParameter(Paths, "只提交这些路径的改动，缺省提交暂存区的全部改动", List: true),
        ],
        [new("git", null), new("commit", null), new("-m", null), new("{message}", null), new("--", Paths), new("{paths}", null)]);
}