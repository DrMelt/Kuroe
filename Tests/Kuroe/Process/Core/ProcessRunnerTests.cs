using Xunit;

namespace Kuroe.Process.Tests;

/// <summary>进程执行的边界：启动失败、输出与退出码、超时终止。</summary>
public sealed class ProcessRunnerTests
{
    [Fact]
    public void Missing_program_reports_a_start_failure()
    {
        ProcessRunner runner = new();

        Assert.Throws<ProcessStartException>(
            () => runner.Run("kuroe-missing-program", [], Path.GetTempPath(), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Output_and_exit_code_are_returned()
    {
        ProcessRunner runner = new();
        (string program, string[] arguments) = Echo("你好");

        ProcessResult result = runner.Run(program, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(10));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("你好", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Process_over_the_timeout_is_terminated()
    {
        ProcessRunner runner = new();
        (string program, string[] arguments) = Sleep();

        ProcessResult result = runner.Run(program, arguments, Path.GetTempPath(), TimeSpan.FromMilliseconds(200));

        Assert.True(result.TimedOut);
    }

    private static (string Program, string[] Arguments) Echo(string text) =>
        OperatingSystem.IsWindows()
            ? ("cmd", ["/c", "echo", text])
            : ("/bin/sh", ["-c", $"echo {text}"]);

    private static (string Program, string[] Arguments) Sleep() =>
        OperatingSystem.IsWindows()
            ? ("cmd", ["/c", "ping", "-n", "6", "127.0.0.1"])
            : ("/bin/sh", ["-c", "sleep 5"]);
}
