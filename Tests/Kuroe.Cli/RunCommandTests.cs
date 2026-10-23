using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>run 命令的驱动语义与退出码。</summary>
public sealed class RunCommandTests
{
    [Fact]
    public async Task Done_flow_streams_text_and_returns_success()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (RunCommand command, StringWriter output, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, "目标");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("输出", output.ToString());
        Assert.EndsWith(Environment.NewLine, output.ToString());
        Assert.Contains("已完成", error.ToString());
    }

    [Theory]
    [InlineData(CliFlows.Input, "Input")]
    [InlineData(CliFlows.Approval, "Approval")]
    public async Task Waiting_node_flow_is_unavailable(string flowsJson, string kind)
    {
        await using CliEnvironment environment = CliEnvironment.Create(flowsJson);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, "目标");

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains(kind, error.ToString());
        Assert.Contains("未注册", error.ToString());
    }

    [Fact]
    public async Task Blocked_flow_returns_blocked()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Blocked);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, "目标");

        Assert.Equal(ExitCodes.Blocked, code);
        Assert.Contains("已阻塞", error.ToString());
    }

    private static (RunCommand Command, StringWriter Output, StringWriter Error) Build(CliEnvironment environment)
    {
        StringWriter output = new();
        StringWriter error = new();
        CliOutput cli = new(output, error);
        RunCommand command = new(environment.Session, cli);

        return (command, output, error);
    }
}