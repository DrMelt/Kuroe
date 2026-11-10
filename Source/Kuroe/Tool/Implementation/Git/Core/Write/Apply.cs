using Kuroe.Tool.Command.Shared;

namespace Kuroe.Tool.Git.Write;

/// <summary>应用补丁的命令定义。</summary>
internal static class Apply
{
    /// <summary>参数名：只判断补丁能否应用。</summary>
    private const string Check = "check";

    /// <summary>参数名：只给补丁的改动统计。</summary>
    private const string Stat = "stat";

    /// <summary>参数名：反向应用补丁。</summary>
    private const string Reverse = "reverse";

    /// <summary>参数名：补丁文件路径。</summary>
    private const string Patch = "patch";

    /// <summary>函数定义与模板：git apply [--check] [--stat] [--reverse] {patch}。补丁路径解析为绝对路径，不会被当成开关。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/write/Apply"),
        "把补丁文件的改动应用到工作树；check 为真时只判断能否应用，stat 为真时只给改动统计。",
        [
            new ToolParameter(new ToolName(Check), "只判断补丁能否应用，不改动文件，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Stat), "只给补丁的改动统计，不应用，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Reverse), "反向应用补丁，布尔，缺省为假", Flag: true),
            new ToolParameter(new ToolName(Patch), "补丁文件路径", Required: true, Path: true),
        ],
        [
            new("git", null),
            new("apply", null),
            new("--check", Check),
            new("--stat", Stat),
            new("--reverse", Reverse),
            new("{patch}", null),
        ]);
}