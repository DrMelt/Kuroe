using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Read;

/// <summary>查看提交历史的命令定义。</summary>
internal static class Log
{
    /// <summary>参数名：提交条数。</summary>
    private const string Count = "count";

    /// <summary>参数名：起始修订或范围。</summary>
    private const string Revision = "revision";

    /// <summary>参数名：作者筛选。</summary>
    private const string Author = "author";

    /// <summary>参数名：提交说明筛选。</summary>
    private const string Grep = "grep";

    /// <summary>参数名：起始时间筛选。</summary>
    private const string Since = "since";

    /// <summary>参数名：结束时间筛选。</summary>
    private const string Until = "until";

    /// <summary>参数名：改动统计开关。</summary>
    private const string Stat = "stat";

    /// <summary>参数名：只看第一父提交。</summary>
    private const string FirstParent = "firstParent";

    /// <summary>参数名：限定查看的路径列表。</summary>
    private const string Paths = "paths";

    /// <summary>参数名：排除的路径列表。</summary>
    private const string ExcludePaths = "excludePaths";

    /// <summary>函数定义与模板：git log --oneline --decorate --max-count={count} [--author={author}]
    /// [--grep={grep}] [--since={since}] [--until={until}] [--stat] [--first-parent] [revision]
    /// -- [paths] [:(exclude){excludePaths}]。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Log"),
        "查看提交历史，单行概述并按新到旧排列；可按作者、说明、时间与修订筛选。",
        [
            new ToolParameter(new ToolName(Count), "要查看的提交条数，正整数", Required: true),
            new ToolParameter(new ToolName(Revision), "查看历史的起始修订、范围或分支，缺省为当前分支"),
            new ToolParameter(new ToolName(Author), "按作者筛选，可写名字或邮箱片段，缺省不筛选"),
            new ToolParameter(new ToolName(Grep), "按提交说明筛选，缺省不筛选"),
            new ToolParameter(new ToolName(Since), "只查看此时间之后的提交，如 2026-01-01，缺省不限制"),
            new ToolParameter(new ToolName(Until), "只查看此时间之前的提交，如 2026-12-31，缺省不限制"),
            new ToolParameter(new ToolName(Stat), "同时给出每个提交的改动统计，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(FirstParent), "只看第一父提交，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Paths), "限定查看的文件或目录路径列表，缺省查看全部", List: true, Path: true, AllowLeadingDash: true),
            new ToolParameter(new ToolName(ExcludePaths), "排除的文件或目录路径列表，缺省不排除", List: true, Path: true, AllowLeadingDash: true),
        ],
        [
            new("git", null),
            new("log", null),
            new("--oneline", null),
            new("--decorate", null),
            new("--max-count={count}", Count),
            new("--author={author}", Author),
            new("--grep={grep}", Grep),
            new("--since={since}", Since),
            new("--until={until}", Until),
            new("--stat", Stat),
            new("--first-parent", FirstParent),
            new("{revision}", null),
            new("--", null),
            new("{paths}", null),
            new(":(exclude){excludePaths}", null),
        ]);
}