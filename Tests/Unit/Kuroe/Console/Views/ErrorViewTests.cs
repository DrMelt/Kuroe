using ErrorOr;
using Kuroe.Console.Views;
using Kuroe.Kernel.Tasks.Shared;
using Kuroe.Host.Platform;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>错误的统一输出与按错误码给出的可操作指引。</summary>
public sealed class ErrorViewTests
{
    [Fact(DisplayName = "报告把代码与说明写入错误流")]
    public void Report_writes_code_and_description_to_the_error_stream()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);
        ErrorView errors = new(terminal);

        errors.Report([Error.Failure("Demo.Code", "出问题了")]);

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("错误：Demo.Code：出问题了", error.Output);
    }

    [Theory]
    [InlineData(ErrorCodes.SettingsModelPath, "模型选择在「模型与提供商」菜单里切换。", Label = "设置模型路径")]
    [InlineData(ErrorCodes.SettingsNullValue, "清除设置用「设置」菜单里的「删除用户层设置」。", Label = "设置空值")]
    [InlineData(FlowErrorCodes.TaskNotFound, "在「任务」菜单里查看任务号。", Label = "任务不存在")]
    [InlineData(FlowErrorCodes.ExecutionNotFound, "执行号全局递增，在「任务」菜单里逐级查看执行。", Label = "执行不存在")]
    [InlineData(FlowErrorCodes.FlowNotFound, "在「流程」菜单里查看流程名。", Label = "流程不存在")]
    [InlineData(FlowErrorCodes.FlowInvalidPath, "「流程」菜单导入时文件路径相对工作目录解析，路径非法时换一个写法。", Label = "流程路径非法")]
    [InlineData(FlowErrorCodes.FlowEntry, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。", Label = "流程入口错误")]
    [InlineData(FlowErrorCodes.FlowComposition, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。", Label = "流程组合错误")]
    [InlineData(FlowErrorCodes.FlowName, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。", Label = "流程名错误")]
    [InlineData(FlowErrorCodes.FlowFormat, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完在「流程」菜单确认是否加载成功。", Label = "流程格式错误")]
    [InlineData(ErrorCodes.ToolDuplicate, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。", Label = "工具重复")]
    [InlineData(ErrorCodes.ToolInvalid, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。", Label = "工具非法")]
    [InlineData(ErrorCodes.ToolFormat, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完在「工具」菜单确认是否加载成功。", Label = "工具格式错误")]
    public void Guide_maps_error_codes_to_hints(string code, string hint)
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure(code, "说明")]);

        Assert.Contains(hint, output.Output);
    }

    [Fact(DisplayName = "没有映射代码的引导不打印")]
    public void Guide_without_a_mapped_code_prints_nothing()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure("Demo.Unmapped", "说明")]);

        Assert.Equal(string.Empty, output.Output);
    }

    [Fact(DisplayName = "未选择模型时引导指向选择")]
    public void Guide_without_a_model_choice_points_to_selection()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(environment.Models, null);

        Assert.Contains("在「模型与提供商」菜单选择模型", output.Output);
    }

    [Fact(DisplayName = "未注册模型的引导给出注册命令")]
    public void Guide_for_an_unregistered_model_names_the_registration_command()
    {
        using TestEnvironment environment = TestEnvironment.Create();
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(environment.Models, "未注册");

        Assert.Contains("在「模型与提供商」菜单的「注册模型」里注册 未注册。", output.Output);
    }

    [Fact(DisplayName = "引导跳过空值与已注册模型")]
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
