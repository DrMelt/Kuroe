using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>删除已跟踪文件的命令定义。</summary>
internal static class Remove
{
    /// <summary>参数名：只把路径移出索引并保留工作树文件。</summary>
    private const string Cached = "cached";

    /// <summary>参数名：要删除的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git rm -r [--cached] -- {paths} [:(exclude){excludePaths}]。-- 之后的路径按路径解析。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Remove"),
        "删除已跟踪的文件；cached 为真时只把路径移出索引并保留工作树文件；可排除部分路径。",
        [
            new ToolParameter(new ToolName(Cached), "只把路径移出索引并保留工作树文件，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Paths), "要删除的文件或目录路径列表", Required: true, List: true, Path: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true),
        ],
        [
            new("git", null),
            new("rm", null),
            new("-r", null),
            new("--cached", Cached),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ]);
}