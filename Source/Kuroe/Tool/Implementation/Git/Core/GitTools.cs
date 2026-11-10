using Kuroe.Shared;
using Kuroe.Tool.Command.Shared;
using Kuroe.Tool.Git.Branch;
using Kuroe.Tool.Git.Read;
using Kuroe.Tool.Git.Write;

namespace Kuroe.Tool.Git;

/// <summary>内置 git 工具面：九个函数都是命令工具定义，模板展开与进程执行复用命令面，命令在路径基准目录执行。</summary>
public static class GitTools
{
    /// <summary>九个函数的命令定义，按只读、当前分支改动与分支操作分组，模板与参数在各函数文件里给出。</summary>
    public static IReadOnlyList<CommandToolDefinition> Definitions { get; } = Create();

    /// <summary>装配定义并校验模板与参数的一致性。</summary>
    private static IReadOnlyList<CommandToolDefinition> Create()
    {
        IReadOnlyList<CommandToolDefinition> definitions =
        [
            Status.Definition with { Variables = [PathVariables.RootName] },
            Diff.Definition with { Variables = [PathVariables.RootName] },
            Log.Definition with { Variables = [PathVariables.RootName] },
            Show.Definition with { Variables = [PathVariables.RootName] },
            Branches.Definition with { Variables = [PathVariables.RootName] },
            Add.Definition with { Variables = [PathVariables.RootName] },
            Commit.Definition with { Variables = [PathVariables.RootName] },
            Restore.Definition with { Variables = [PathVariables.RootName] },
            Switch.Definition with { Variables = [PathVariables.RootName] },
        ];

        CommandToolValidation.EnsureValid(definitions);

        return definitions;
    }
}