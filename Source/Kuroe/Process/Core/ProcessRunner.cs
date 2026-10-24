using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace Kuroe.Process;

/// <summary>用系统进程执行命令。stdout 与 stderr 的行汇入同一份文本，顺序不保；超时终止整个进程树。</summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <summary>在工作目录启动程序并等待结果，输出按 UTF-8 解码。超时以 TimedOut 交回，无法启动时抛 <see cref="ProcessStartException"/>。</summary>
    public ProcessResult Run(
        string program,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TimeSpan timeout)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = program,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            CreateNoWindow = true,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new DiagnosticsProcess { StartInfo = startInfo };
        object gate = new();
        var output = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => AppendData(output, eventArgs.Data, gate);
        process.ErrorDataReceived += (_, eventArgs) => AppendData(output, eventArgs.Data, gate);

        try
        {
            if (!process.Start())
            {
                throw new ProcessStartException($"无法启动 {program}。");
            }
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            throw new ProcessStartException($"无法启动 {program}：{ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        int timeoutMilliseconds = (int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue);
        if (!process.WaitForExit(timeoutMilliseconds))
        {
            KillTree(process);
            process.WaitForExit(5000);
            return new ProcessResult(output.ToString(), 0, TimedOut: true);
        }

        process.WaitForExit();

        return new ProcessResult(output.ToString(), process.ExitCode, TimedOut: false);
    }

    /// <summary>stdout 与 stderr 的行汇入同一份文本，顺序不保。两路回调可能并行，写入加锁。</summary>
    private static void AppendData(StringBuilder output, string? data, object gate)
    {
        if (data is { Length: > 0 })
        {
            lock (gate)
            {
                output.AppendLine(data);
            }
        }
    }

    /// <summary>终止整个进程树，尽力而为，失败不阻断超时返回。</summary>
    private static void KillTree(DiagnosticsProcess process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // 进程可能已退出或已被系统清理，不追加错误
        }
    }
}