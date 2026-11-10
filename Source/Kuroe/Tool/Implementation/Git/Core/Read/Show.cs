using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看单个提交的命令定义。</summary>
internal static class Show
{
    /// <summary>参数名：提交号或修订号。</summary>
    private const string Revision = "revision";

    /// <summary>参数名：限定查看的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：改动统计开关。</summary>
    private const string Stat = "stat";

    /// <summary>参数名：只看第一父提交。</summary>
    private const string FirstParent = "firstParent";

    /// <summary>函数定义与模板：git show [--stat] [--first-parent] {revision} [-- paths]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Show"),
        "查看一个提交的说明与改动；可只看统计或限定路径。",
        [
            new ToolParameter(new ToolName(Revision), "要查看的提交号或修订号", Required: true),
            new ToolParameter(new ToolName(Stat), "只给改动统计，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(FirstParent), "合并提交只看第一父的改动，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Paths), "限定查看的文件或目录路径列表，缺省查看全部", List: true, Path: true),
        ],
        [
            new("git", null),
            new("show", null),
            new("--stat", Stat),
            new("--first-parent", FirstParent),
            new("{revision}", null),
            new("--", Paths),
            new("{paths}", null),
        ]);
}