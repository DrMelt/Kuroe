using Kuroe.Tool.Command;
using Kuroe.Tool.Command.Model;
using Kuroe.Tool.Git.Read;
using Kuroe.Tool.Git.Write;

namespace Kuroe.Tool.Git;

/// <summary>内置 git 工具面：九个函数都是命令工具定义，模板展开与进程执行复用命令面，命令在基准目录执行。</summary>
public static class GitTools
{
    /// <summary>九个函数的命令定义，按只读与改动分组，模板与参数在各函数文件里给出。</summary>
    public static IReadOnlyList<CommandToolDefinition> Definitions { get; } =
    [
        Status.Definition,
        Diff.Definition,
        Log.Definition,
        Show.Definition,
        Branches.Definition,
        Add.Definition,
        Commit.Definition,
        Switch.Definition,
        Restore.Definition,
    ];

    /// <summary>把定义装配成在基准目录执行的工具载体。</summary>
    public static IReadOnlyList<ITool> Tools(string baseDirectory) =>
        [.. Definitions.Select(definition => new CommandTool(definition, baseDirectory))];
}