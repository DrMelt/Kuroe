using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看工作树改动状态的命令定义。</summary>
internal static class Status
{
    /// <summary>参数名：限定查看的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：未跟踪文件的显示方式。</summary>
    private const string Untracked = "untracked";

    /// <summary>函数定义与模板：git status --short --branch [--untracked-files={untracked}] [-- paths]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Status"),
        "查看工作树的改动状态：当前分支与各项的暂存、未暂存、未跟踪情况。",
        [
            new ToolParameter(new ToolName(Untracked), "未跟踪文件的显示方式，取 all、normal 或 no，缺省为 normal"),
            new ToolParameter(new ToolName(Paths), "限定查看的文件或目录路径列表，缺省查看全部", List: true, Path: true, AllowLeadingDash: true),
        ],
        [
            new("git", null),
            new("status", null),
            new("--short", null),
            new("--branch", null),
            new("--untracked-files={untracked}", Untracked),
            new("--", Paths),
            new("{paths}", null),
        ]);
}