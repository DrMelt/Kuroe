using Kuroe.Cli.Commands;
using Kuroe.Cli.Tests.Support;
using Xunit;

namespace Kuroe.Cli.Tests.Commands;

/// <summary>config 命令写入与删除用户层设置。</summary>
public sealed class ConfigCommandsTests
{
    [Fact(DisplayName = "设置与取消设置用户配置往返")]
    public async Task Set_and_unset_round_trip_user_settings()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ConfigCommands command, StringWriter output, StringWriter error) = Build(environment);

        int set = command.Set("Runtime:LogLevel", "Debug");

        Assert.Equal(ExitCodes.Success, set);
        Assert.Equal("Debug", environment.Session.Settings.TryGetUserValue("Runtime:LogLevel"));
        Assert.Contains("已保存", output.ToString());
        Assert.Empty(error.ToString());

        int unset = command.Unset("Runtime:LogLevel");

        Assert.Equal(ExitCodes.Success, unset);
        Assert.Null(environment.Session.Settings.TryGetUserValue("Runtime:LogLevel"));
    }

    [Fact(DisplayName = "助手语言变更报告只影响控制台")]
    public async Task Assistant_language_change_reports_console_only()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ConfigCommands command, StringWriter output, _) = Build(environment);

        int code = command.Set("Assistant:Language", "English");

        Assert.Equal(ExitCodes.Success, code);
        Assert.Contains("只作用于 Kuroe.Console", output.ToString());
    }

    [Fact(DisplayName = "未知设置路径被拒绝")]
    public async Task Unknown_setting_path_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ConfigCommands command, _, StringWriter error) = Build(environment);

        int code = command.Set("Nope:Value", "1");

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("错误：", error.ToString());
    }

    [Fact(DisplayName = "模型选择路径被拒绝")]
    public async Task Model_selection_path_is_rejected()
    {
        await using CliEnvironment environment = CliEnvironment.Create(CliFlows.Done);
        (ConfigCommands command, _, StringWriter error) = Build(environment);

        int code = command.Set("Runtime:Model", "fake");

        Assert.Equal(ExitCodes.Failure, code);
        Assert.Contains("Settings.ModelPath", error.ToString());
    }

    private static (ConfigCommands Command, StringWriter Output, StringWriter Error) Build(CliEnvironment environment)
    {
        StringWriter output = new();
        StringWriter error = new();
        ConfigCommands command = new(environment.Session, new CliOutput(output, error));

        return (command, output, error);
    }
}