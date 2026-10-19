using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看单个提交的命令定义。</summary>
internal static class Show
{
    /// <summary>参数名：提交号或修订号。</summary>
    private const string Revision = "revision";

    /// <summary>函数定义与模板：git show {revision}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("git/read/Show"),
            "查看一个提交的说明与改动，revision 是提交号、分支名或 HEAD 之类的修订号。",
            [new ToolParameter(new ToolName(Revision), "要查看的提交号或修订号", Required: true)]),
        [new("git", null), new("show", null), new("{revision}", null)],
        null,
        CommandToolTemplate.DefaultTimeoutSeconds,
        CommandToolTemplate.DefaultOutputLimit);
}