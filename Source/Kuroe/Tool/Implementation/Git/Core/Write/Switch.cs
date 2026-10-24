using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>切换分支的命令定义。</summary>
internal static class Switch
{
    /// <summary>参数名：分支名。</summary>
    private const string Branch = "branch";

    /// <summary>参数名：是否新建分支。</summary>
    private const string Create = "create";

    /// <summary>函数定义与模板：git switch [-c] {branch}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Switch"),
        "切到指定分支，create 为真时新建分支并切过去。",
        [
            new ToolParameter(new ToolName(Branch), "要切换到的分支名", Required: true),
            new ToolParameter(new ToolName(Create), "新建分支并切过去，布尔，缺省为假", Flag: true),
        ],
        [new("git", null), new("switch", null), new("-c", Create), new("{branch}", null)]);
}