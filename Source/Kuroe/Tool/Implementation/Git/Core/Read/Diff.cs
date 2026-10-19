using Kuroe.Tool.Command.Model;

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

    /// <summary>函数定义与模板：git diff [--staged] [revision] [-- paths]，-- 与路径项一起省略。</summary>
    internal static readonly CommandToolDefinition Definition = new(
        new ToolPath("git/read/Diff"),
        "查看改动的差异：缺省比较工作树与索引，staged 为真时比较索引与 HEAD，revision 给定时与它比较，paths 限定路径。",
        [
            new CommandToolParameter(Staged, "与暂存区比较而不是与工作树比较，布尔，缺省为假", Flag: true),
            new CommandToolParameter(Revision, "比较的修订号，如 HEAD、main、提交号，缺省与索引比较"),
            new CommandToolParameter(Paths, "限定查看的文件或目录路径列表，缺省查看全部", List: true),
        ],
        [new("git", null), new("diff", null), new("--staged", Staged), new("{revision}", null), new("--", Paths), new("{paths}", null)]);
}