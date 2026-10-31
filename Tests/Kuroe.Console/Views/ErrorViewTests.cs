using ErrorOr;
using Kuroe.Console.Views;
using Kuroe.Flow.Shared;
using Kuroe.Shared;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>错误的统一输出与按错误码给出的可操作指引。</summary>
public sealed class ErrorViewTests
{
    [Fact]
    public void Report_writes_code_and_description_to_the_error_stream()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);
        ErrorView errors = new(terminal);

        errors.Report([Error.Failure("Demo.Code", "出问题了")]);

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("错误：Demo.Code：出问题了", error.Output);
    }

    [Theory]
    [InlineData(ErrorCodes.SettingsModelPath, "模型选择在「模型与提供商」菜单里切换。")]
    [InlineData(ErrorCodes.SettingsNullValue, "清除设置用「设置」菜单里的「删除用户层设置」。")]
    [InlineData(FlowErrorCodes.TaskNotFound, "在「任务」菜单里查看任务号。")]
    [InlineData(FlowErrorCodes.ExecutionNotFound, "执行号全局递增，在「任务」菜单里逐级查看执行。")]
    [InlineData(FlowErrorCodes.FlowNotFound, "在「流程」菜单里查看流程名。")]
    [InlineData(FlowErrorCodes.FlowInvalidPath, "「流程」菜单导入时文件路径相对工作目录解析，路径非法时换一个写法。")]
    [InlineData(FlowErrorCodes.FlowNode, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowBody, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowName, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowFormat, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolDuplicate, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolInvalid, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolFormat, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。")]
    public void Guide_maps_error_codes_to_hints(string code, string hint)
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure(code, "说明")]);

        Assert.Contains(hint, output.Output);
    }

    [Fact]
    public void Guide_without_a_mapped_code_prints_nothing()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure("Demo.Unmapped", "说明")]);

        Assert.Equal(string.Empty, output.Output);
    }

    [Fact]
    public void Guide_without_a_model_choice_points_to_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(environment.Models, null);

        Assert.Contains("在「模型与提供商」菜单选择模型", output.Output);
    }

    [Fact]
    public void Guide_for_an_unregistered_model_names_the_registration_command()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(environment.Models, "未注册");

        Assert.Contains("在「模型与提供商」菜单的「注册模型」里注册 未注册。", output.Output);
    }

    [Fact]
    public void Guide_skips_blank_and_registered_models()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(environment.Models, "   ");
        errors.GuideModelRegistration(environment.Models, "fake");

        Assert.Equal(string.Empty, output.Output);
    }
}
