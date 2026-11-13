using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;
using Xunit;

namespace Kuroe.Cli.Tests.Commands;

/// <summary>run 命令的驱动语义与退出码。</summary>
public sealed class RunCommandTests
{
    [Fact]
    public async Task Done_flow_streams_text_and_returns_success()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (RunCommand command, StringWriter output, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

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

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains(kind, error.ToString());
        Assert.Contains("未注册", error.ToString());
    }

    [Fact]
    public async Task Blocked_flow_returns_blocked()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Blocked);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Blocked, code);
        Assert.Contains("已阻塞", error.ToString());
    }

    [Fact]
    public async Task Multiple_inputs_are_given_by_port_name()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.MultiStart);
        (RunCommand command, StringWriter output, _) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=写致辞", "风格=口语"]);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("输出", output.ToString());
    }

    [Fact]
    public async Task Missing_start_input_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.MultiStart);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=写致辞"]);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("开始端口 风格 没有输入。", error.ToString());
    }

    [Fact]
    public async Task Host_start_port_input_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.HostInput);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=目标", "启动路径=."]);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("开始端口 启动路径 由宿主提供，不接受提交输入。", error.ToString());
    }

    [Fact]
    public async Task Input_without_port_name_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标"]);

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("端口=值", error.ToString());
    }

    [Fact]
    public async Task Done_flow_reports_requests_tokens_and_context()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        environment.Runner.Usage = new Kuroe.Execution.Shared.ExecutionUsage(
            2,
            0,
            new Kuroe.Execution.Shared.TokenUsage(100, 20, 120, 80, 5))
        {
            ContextTokens = 75,
        };
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("请求 2 次", error.ToString());
        Assert.Contains("输入 100（83%），缓存输入 80（占输入 80%）", error.ToString());
        Assert.Contains("输出 20（17%），推理 5（占输出 25%）", error.ToString());
        Assert.Contains("合计 120", error.ToString());
        Assert.Contains("上下文 75", error.ToString());
    }

    [Fact]
    public async Task Done_flow_reports_unknown_context()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        environment.Runner.Usage = new Kuroe.Execution.Shared.ExecutionUsage(
            1,
            1,
            Kuroe.Execution.Shared.TokenUsage.None);
        (RunCommand command, _, StringWriter error) = Build(environment);

        int code = await command.RunAsync("默认", null, ["目标=目标"]);

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("请求 1 次（均未返回用量）", error.ToString());
        Assert.Contains("用量 未提供", error.ToString());
        Assert.Contains("上下文 未提供", error.ToString());
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