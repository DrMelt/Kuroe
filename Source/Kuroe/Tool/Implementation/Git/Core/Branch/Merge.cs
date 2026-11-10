using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Branch;

/// <summary>合并修订的命令定义。</summary>
internal static class Merge
{
    /// <summary>参数名：只在能快进时合并。</summary>
    private const string FastForwardOnly = "fastForwardOnly";

    /// <summary>参数名：总是生成合并提交。</summary>
    private const string NoFastForward = "noFastForward";

    /// <summary>参数名：要合并的修订。</summary>
    private const string Revision = "revision";

    /// <summary>参数名：中止进行中的合并。</summary>
    private const string Abort = "abort";

    /// <summary>参数名：继续进行中的合并。</summary>
    private const string Continue = "continue";

    /// <summary>函数定义与模板：git -c core.editor=: merge [--ff-only | --no-ff] [revision] [--abort | --continue]。
    /// core.editor 固定为 :，进程非交互，合并提交不打开编辑器。revision 与 abort、continue 互斥；
    /// 快进开关也不能与 abort、continue 同时给出，收尾模式只带自己的开关。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/branch/Merge"),
        "把指定分支或提交合并进当前分支；可中止或继续一次进行中的合并。",
        [
            new ToolParameter(new ToolName(FastForwardOnly), "只在能快进时合并，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(NoFastForward), "总是生成合并提交，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Revision), "要合并的分支或提交，与 abort、continue 互斥"),
            new ToolParameter(new ToolName(Abort), "中止进行中的合并，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Continue), "继续进行中的合并，布尔，缺省为假", Flag: true),
        ],
        [
            new("git", null),
            new("-c", null),
            new("core.editor=:", null),
            new("merge", null),
            new("--ff-only", FastForwardOnly),
            new("--no-ff", NoFastForward),
            new("{revision}", Revision),
            new("--abort", Abort),
            new("--continue", Continue),
        ],
        Groups:
        [
            new ToolParameterGroup([new ToolName(Revision), new ToolName(Abort), new ToolName(Continue)]),
            new ToolParameterGroup(
                [new ToolName(FastForwardOnly), new ToolName(NoFastForward), new ToolName(Abort), new ToolName(Continue)]),
        ]);
}