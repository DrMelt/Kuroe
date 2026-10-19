using Kuroe.Tool.Command.Model;

namespace Kuroe.Tool.Git.Write;

/// <summary>把改动放入暂存区的命令定义。</summary>
internal static class Add
{
    /// <summary>参数名：要暂存的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>函数定义与模板：git add -- {paths}。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolDefinition(
            new ToolPath("git/write/Add"),
            "把指定路径的改动放入暂存区，供 Commit 提交。",
            [new ToolParameter(new ToolName(Paths), "要暂存的文件或目录路径列表", Required: true, List: true)]),
        [new("git", null), new("add", null), new("--", null), new("{paths}", null)],
        null,
        CommandToolTemplate.DefaultTimeoutSeconds,
        CommandToolTemplate.DefaultOutputLimit);
}