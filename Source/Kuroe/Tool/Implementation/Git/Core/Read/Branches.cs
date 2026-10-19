using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Git.Read;

/// <summary>列出分支的命令定义。</summary>
internal static class Branches
{
    /// <summary>函数定义与模板：git branch --all --verbose，无参数。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("git/read/Branches"),
            "列出全部分支及其当前提交，含远端分支。",
            []),
        [new("git", null), new("branch", null), new("--all", null), new("--verbose", null)],
        null,
        CommandToolTemplate.DefaultTimeoutSeconds,
        CommandToolTemplate.DefaultOutputLimit);
}