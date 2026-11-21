using System.CommandLine;
using Kuroe.Cli.Tests.Support;

using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>命令执行边界：工作目录解析、会话装配与命令树调用。</summary>
public sealed class CliApplicationTests
{
    [Fact(DisplayName = "无效工作目录在会话前失败")]
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

    [Fact(DisplayName = "会话创建异常报告内部错误码")]
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

    [Fact(DisplayName = "流程清单经命令树运行")]
    public async Task Flow_list_runs_through_the_command_tree()
    {
        string root = Path.Combine(Path.GetTempPath(), "kuroe-cli-tests", Guid.NewGuid().ToString("N"));
        Kuroe.TestSupport.InMemoryWorkspaceFiles files = new();
        files.AddFile(Path.Combine(root, ".kuroe", "flows.json"), CliFlows.Done);

        StringWriter output = new();
        StringWriter error = new();
        CliApplication application = new(
            output,
            error,
            directory => CliSession.Create(
                directory,
                compositionOptions: new Kuroe.Composition.KuroeCompositionOptions
                {
                    Files = files,
                    LeaseStores = new Kuroe.TestSupport.InMemoryTaskLeaseStores().Create,
                }));

        RootCommand rootCommand = CliCommandLine.Build(application);
        int code = await rootCommand.Parse(["flow", "list", "-d", root]).InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("默认", output.ToString());
        Assert.Empty(error.ToString());
    }
}