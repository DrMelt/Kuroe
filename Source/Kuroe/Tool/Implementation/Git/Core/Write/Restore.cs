using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Git.Write;

/// <summary>还原路径改动的命令定义。</summary>
internal static class Restore
{
    /// <summary>参数名：把路径移出暂存区而不是丢弃工作树的改动。</summary>
    private const string Staged = "staged";

    /// <summary>参数名：要还原的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>函数定义与模板：git restore [--staged] -- {paths}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Restore"),
        "丢弃指定路径的改动：缺省丢弃工作树的改动，staged 为真时把路径移出暂存区并保留工作树内容。",
        [
            new CommandToolParameter(Staged, "把路径移出暂存区而不是丢弃工作树的改动，布尔，缺省为假", Flag: true),
            new CommandToolParameter(Paths, "要还原的文件或目录路径列表", Required: true, List: true),
        ],
        [new("git", null), new("restore", null), new("--staged", Staged), new("--", null), new("{paths}", null)]);
}