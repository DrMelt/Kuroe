using System.CommandLine;
using Kuroe.Cli.Tests.Support;

using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>命令执行边界：工作目录解析、会话装配与命令树调用。</summary>
public sealed class CliApplicationTests
{
    [Fact]
    public async Task Invalid_work_directory_fails_before_session()
    {
        StringWriter output = new();
        StringWriter error = new();
        CliApplication application = new(
            output,
            error,
            directory => CliSession.Create(directory));

        int code = await application.ExecuteAsync("bad\u0000path", (_, _) => ExitCodes.Success);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Empty(output.ToString());
        Assert.Contains("WorkDirectory.Invalid", error.ToString());
    }

    [Fact]
    public async Task Session_creation_exception_reports_the_internal_code()
    {
        StringWriter output = new();
        StringWriter error = new();
        CliApplication application = new(
            output,
            error,
            _ => throw new InvalidOperationException("boom"));

        int code = await application.ExecuteAsync(null, (_, _) => ExitCodes.Success);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("Cli.Internal", error.ToString());
        Assert.Contains("boom", error.ToString());
    }

    [Fact]
    public async Task Flow_list_runs_through_the_command_tree()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-cli-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, ".kuroe"));
        await File.WriteAllTextAsync(Path.Combine(root, ".kuroe", "flows.json"), CliFlows.Done);
        try
        {
            StringWriter output = new();
            StringWriter error = new();
            CliApplication application = new(
                output,
                error,
                directory => CliSession.Create(directory));

            RootCommand rootCommand = CliCommandLine.Build(application);
            int code = await rootCommand.Parse(["flow", "list", "-d", root]).InvokeAsync();

            Assert.Equal(ExitCodes.Success, code);
            Assert.Contains("默认", output.ToString());
            Assert.Empty(error.ToString());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}