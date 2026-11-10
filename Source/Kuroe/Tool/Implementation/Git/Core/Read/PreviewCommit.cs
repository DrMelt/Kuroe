using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>预演提交的命令定义。</summary>
internal static class PreviewCommit
{
    /// <summary>参数名：限定摘要的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git commit --dry-run --short --branch [-- {paths}] [:(exclude){excludePaths}]。
    /// dry-run 固定在模板里，没有关闭开关；不接收提交说明，不生成提交。输出是短格式状态列表，作为提交摘要。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/PreviewCommit"),
        "给出提交摘要：列出将提交、有本地改动但不会被提交与未跟踪的路径，不生成提交。",
        [
            new ToolParameter(new ToolName(Paths), "限定摘要的文件或目录路径列表，缺省给出全部改动", List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，只在给出 paths 时生效", List: true, Path: true),
        ],
        [
            new("git", null),
            new("commit", null),
            new("--dry-run", null),
            new("--short", null),
            new("--branch", null),
            new("--", Paths),
            new("{paths}", null),
            new(":(exclude){excludePaths}", Paths),
        ]);
}