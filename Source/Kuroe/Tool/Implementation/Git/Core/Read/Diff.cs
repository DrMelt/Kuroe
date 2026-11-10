using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看改动的命令定义。</summary>
internal static class Diff
{
    /// <summary>参数名：与暂存区比较而不是与工作树比较。</summary>
    private const string Staged = "staged";

    /// <summary>参数名：比较的修订号。</summary>
    private const string Revision = "revision";

    /// <summary>参数名：限定查看的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>参数名：只给改动统计。</summary>
    private const string Stat = "stat";

    /// <summary>参数名：只给改动文件列表。</summary>
    private const string NameOnly = "nameOnly";

    /// <summary>参数名：给改动文件列表与状态。</summary>
    private const string NameStatus = "nameStatus";

    /// <summary>参数名：按改动类型筛选。</summary>
    private const string DiffFilter = "diffFilter";

    /// <summary>函数定义与模板：git diff [--staged] [--stat] [--name-only | --name-status]
    /// [--diff-filter={diffFilter}] [revision] -- [paths] [:(exclude){excludePaths}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Diff"),
        "查看改动的差异，缺省比较工作树与索引；可按改动类型筛选并排除路径。",
        [
            new ToolParameter(new ToolName(Staged), "与暂存区比较而不是与工作树比较，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Stat), "只给改动统计，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(NameOnly), "只给改动文件列表，与 nameStatus 二选一，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(NameStatus), "给改动文件列表与状态，与 nameOnly 二选一，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(DiffFilter), "按改动类型筛选，如 A、M、D，缺省不筛选"),
            new ToolParameter(new ToolName(Revision), "比较的修订号，如 HEAD、main、提交号，也可写 A..B，缺省与索引比较"),
            new ToolParameter(new ToolName(Paths), "限定查看的文件或目录路径列表，缺省查看全部", List: true, Path: true, AllowLeadingDash: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true, AllowLeadingDash: true),
        ],
        [
            new("git", null),
            new("diff", null),
            new("--staged", Staged),
            new("--stat", Stat),
            new("--name-only", NameOnly),
            new("--name-status", NameStatus),
            new("--diff-filter={diffFilter}", DiffFilter),
            new("{revision}", null),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ],
        Groups: [new ToolParameterGroup([new ToolName(NameOnly), new ToolName(NameStatus)])]);
}