using System.CommandLine;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>命令树的解析规则。</summary>
public sealed class CliCommandLineTests
{
    [Fact]
    public void Known_commands_parse()
    {
        Assert.Empty(Build().Parse(["flow", "show", "默认"]).Errors);
        Assert.Empty(Build().Parse(["run", "--flow", "默认", "目标"]).Errors);
        Assert.Empty(Build().Parse(["model", "use", "gpt"]).Errors);
        Assert.Empty(Build().Parse(["config", "set", "Runtime:Temperature", "0.7"]).Errors);
    }

    [Fact]
    public void Provider_credential_argument_is_optional()
    {
        Assert.Empty(Build().Parse(["provider", "add", "p", "https://example.invalid/v1"]).Errors);
        Assert.Empty(Build().Parse(["provider", "key", "p"]).Errors);
    }

    [Fact]
    public void Missing_flow_option_is_a_parse_error()
    {
        Assert.NotEmpty(Build().Parse(["run", "目标"]).Errors);
    }

    [Fact]
    public void Assistant_command_is_not_available()
    {
        Assert.NotEmpty(Build().Parse(["ask", "你好"]).Errors);
    }

    [Theory]
    [InlineData("--answer")]
    [InlineData("--answer=背景")]
    [InlineData("-a")]
    [InlineData("--approve")]
    [InlineData("-y")]
    public void Waiting_node_options_report_the_cli_rule(string token)
    {
        ParseResult result = Build().Parse(["run", "--flow", "默认", token, "目标"]);

        Assert.Contains(result.Errors,
            error => error.Message.Contains("run 不接受 --answer 或 --approve", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("--answer")]
    [InlineData("--approve")]
    public void Waiting_node_option_without_value_is_rejected(string token)
    {
        ParseResult result = Build().Parse(["run", "--flow", "默认", "目标", token]);

        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("--answer")]
    [InlineData("-a")]
    [InlineData("--approve")]
    [InlineData("-y")]
    public void Waiting_node_literals_are_goal_text_after_double_dash(string token)
    {
        ParseResult result = Build().Parse(["run", "--flow", "默认", "--", token]);

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Work_directory_option_is_recursive()
    {
        RootCommand root = Build();
        ParseResult result = root.Parse(["flow", "list", "-d", "workspace"]);

        Assert.Empty(result.Errors);
        Assert.Equal("workspace", result.GetValue(root.Options.OfType<Option<string?>>().Single()));
    }

    private static RootCommand Build()
    {
        CliApplication application = new(
            new StringWriter(),
            new StringWriter(),
            _ => throw new InvalidOperationException("解析测试不装配会话。"));

        return CliCommandLine.Build(application);
    }
}