using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看工作树改动状态的命令定义。</summary>
internal static class Status
{
    /// <summary>函数定义与模板：git status --short --branch，无参数。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("git/read/Status"),
            "查看工作树的改动状态：当前分支与各项的暂存、未暂存、未跟踪情况。",
            []),
        [new("git", null), new("status", null), new("--short", null), new("--branch", null)],
        null,
        CommandToolTemplate.DefaultTimeoutSeconds,
        CommandToolTemplate.DefaultOutputLimit);
}