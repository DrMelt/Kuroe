using Xunit;

namespace Kuroe.Process.Tests;

/// <summary>进程执行的边界：启动失败、输出与退出码。</summary>
public sealed class ProcessRunnerTests
{
    [Fact]
    public void Missing_program_reports_a_start_failure()
    {
        ProcessRunner runner = new();

        Assert.Throws<ProcessStartException>(
            () => runner.Run("kuroe-missing-program", [], Path.GetTempPath(), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Output_and_exit_code_are_returned()
    {
        ProcessRunner runner = new();
        (string program, string[] arguments) = Echo("kuroe-output");

        ProcessResult result = runner.Run(program, arguments, Path.GetTempPath(), TimeSpan.FromSeconds(30));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("kuroe-output", result.Output, StringComparison.Ordinal);
    }

    private static (string Program, string[] Arguments) Echo(string text) =>
        OperatingSystem.IsWindows()
            ? ("cmd", ["/c", "echo", text])
            : ("/bin/sh", ["-c", $"echo {text}"]);
}
