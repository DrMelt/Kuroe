using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>列出分支的命令定义。</summary>
internal static class Branches
{
    /// <summary>参数名：分支名筛选模式。</summary>
    private const string Pattern = "pattern";

    /// <summary>函数定义与模板：git branch --all --verbose --list [{pattern}]。--list 是开关，带模式时按模式筛选。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Branches"),
        "列出全部分支及其当前提交，含远端分支；可按名字模式筛选。",
        [new ToolParameter(new ToolName(Pattern), "分支名筛选模式，如 main*，缺省列出全部")],
        [
            new("git", null),
            new("branch", null),
            new("--all", null),
            new("--verbose", null),
            new("--list", null),
            new("{pattern}", null),
        ]);
}