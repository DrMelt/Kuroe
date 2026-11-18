using Kuroe.Process;
using Xunit;

namespace Kuroe.Integration.Tests.Process;

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

        ProcessResult result = runner.Run("dotnet", ["--version"], Path.GetTempPath(), TimeSpan.FromSeconds(30));

        Assert.False(result.TimedOut);
        Assert.Equal(0, result.ExitCode);
        Assert.Matches(@"^\d+\.\d+", result.Output.Trim());
    }
}
