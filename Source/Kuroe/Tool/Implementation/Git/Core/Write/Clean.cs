using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>清理未跟踪文件与目录的命令定义。</summary>
internal static class Clean
{
    /// <summary>参数名：只列出将删除的条目。</summary>
    private const string DryRun = "dryRun";

    /// <summary>参数名：实际删除。</summary>
    private const string Force = "force";

    /// <summary>参数名：要清理的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>函数定义与模板：git clean -d [--dry-run] [--force] -- {paths}。-- 之后的路径按路径解析。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Clean"),
        "清理指定路径下的未跟踪文件与目录；dryRun 为真时只列出将删除的条目，force 为真时才实际删除。",
        [
            new ToolParameter(new ToolName(DryRun), "只列出将删除的条目而不删除，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Force), "实际删除；与 dryRun 同时给出时只预演，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Paths), "要清理的文件或目录路径列表", Required: true, List: true, Path: true),
        ],
        [
            new("git", null),
            new("clean", null),
            new("-d", null),
            new("--dry-run", DryRun),
            new("--force", Force),
            new("--", null),
            new("{paths}", null),
        ]);
}