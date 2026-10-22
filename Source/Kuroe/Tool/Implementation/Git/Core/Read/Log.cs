using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看提交历史的命令定义。</summary>
internal static class Log
{
    /// <summary>参数名：提交条数。</summary>
    private const string Count = "count";

    /// <summary>参数名：限定查看的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>函数定义与模板：git log --oneline --decorate --max-count={count} [-- paths]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Log"),
        "查看提交历史，单行概述并按新到旧排列，count 是条数，paths 限定路径。",
        [
            new CommandToolParameter(Count, "要查看的提交条数，正整数", Required: true),
            new CommandToolParameter(Paths, "限定查看的文件或目录路径列表，缺省查看全部", List: true, Path: true),
        ],
        [
            new("git", null),
            new("log", null),
            new("--oneline", null),
            new("--decorate", null),
            new("--max-count={count}", Count),
            new("--", Paths),
            new("{paths}", null),
        ]);
}