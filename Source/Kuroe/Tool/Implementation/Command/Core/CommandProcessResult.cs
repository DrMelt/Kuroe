namespace Kuroe.Tool.Command;

/// <summary>一次命令进程的执行结果。</summary>
/// <param name="Output">stdout 与 stderr 合并后的文本，保留原始行尾。</param>
/// <param name="ExitCode">进程退出码，TimedOut 为真时无意义。</param>
/// <param name="TimedOut">进程在超时内未结束并被终止。</param>
public readonly record struct CommandProcessResult(string Output, int ExitCode, bool TimedOut);