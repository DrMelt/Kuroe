using ErrorOr;

namespace Kuroe.Tool.Command;

/// <summary>命令工具的进程执行边界：启动程序、收集合并输出并等待结束。</summary>
public interface ICommandProcessRunner
{
    /// <summary>在指定目录启动程序并等待结束。超时由实现终止进程并以 TimedOut 交回，无法启动时返回 ToolErrors.Execute。</summary>
    /// <param name="program">程序路径或命令名。</param>
    /// <param name="arguments">传给程序的参数，不经 shell 解释。</param>
    /// <param name="workingDirectory">进程的工作目录。</param>
    /// <param name="timeout">等待上限。</param>
    ErrorOr<CommandProcessResult> Run(
        string program,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout);
}