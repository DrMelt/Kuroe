using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>预演删除的命令定义。</summary>
internal static class PreviewRemove
{
    /// <summary>参数名：只把路径移出索引并保留工作树文件。</summary>
    private const string Cached = "cached";

    /// <summary>参数名：要预演删除的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git rm -r --dry-run [--cached] -- {paths} [:(exclude){excludePaths}]。
    /// dry-run 固定在模板里，没有关闭开关。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/PreviewRemove"),
        "预演删除已跟踪的文件，列出将删除的文件，不改动索引与工作树。",
        [
            new ToolParameter(new ToolName(Cached), "只把路径移出索引并保留工作树文件，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Paths), "要预演删除的文件或目录路径列表", Required: true, List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true),
        ],
        [
            new("git", null),
            new("rm", null),
            new("-r", null),
            new("--dry-run", null),
            new("--cached", Cached),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ]);
}