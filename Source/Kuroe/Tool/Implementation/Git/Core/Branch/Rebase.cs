using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Branch;

/// <summary>变基的命令定义。</summary>
internal static class Rebase
{
    /// <summary>参数名：变基前后自动暂存与恢复工作树改动。</summary>
    private const string Autostash = "autostash";

    /// <summary>参数名：变基到的上游。</summary>
    private const string Upstream = "upstream";

    /// <summary>参数名：中止进行中的变基。</summary>
    private const string Abort = "abort";

    /// <summary>参数名：继续进行中的变基。</summary>
    private const string Continue = "continue";

    /// <summary>参数名：跳过当前提交并继续。</summary>
    private const string Skip = "skip";

    /// <summary>函数定义与模板：git -c core.editor=: rebase [--autostash] [upstream] [--abort | --continue | --skip]。
    /// core.editor 固定为 :，进程非交互，继续变基时不打开编辑器。upstream 与 abort、continue、skip 互斥；
    /// autostash 也不能与 abort、continue、skip 同时给出，收尾模式只带自己的开关。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/branch/Rebase"),
        "把当前分支的提交变基到上游；可中止、继续或跳过一次进行中的变基。",
        [
            new ToolParameter(new ToolName(Autostash), "变基前后自动暂存与恢复工作树改动，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Upstream), "变基到的上游分支或提交，与 abort、continue、skip 互斥"),
            new ToolParameter(new ToolName(Abort), "中止进行中的变基，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Continue), "继续进行中的变基，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Skip), "跳过当前的提交并继续，布尔，缺省为假", Flag: true),
        ],
        [
            new("git", null),
            new("-c", null),
            new("core.editor=:", null),
            new("rebase", null),
            new("--autostash", Autostash),
            new("{upstream}", Upstream),
            new("--abort", Abort),
            new("--continue", Continue),
            new("--skip", Skip),
        ],
        Groups:
        [
            new ToolParameterGroup(
                [new ToolName(Upstream), new ToolName(Abort), new ToolName(Continue), new ToolName(Skip)]),
            new ToolParameterGroup(
                [new ToolName(Autostash), new ToolName(Abort), new ToolName(Continue), new ToolName(Skip)]),
        ]);
}