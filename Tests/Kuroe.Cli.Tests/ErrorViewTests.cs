using ErrorOr;
using Kuroe.Cli.Views;
using Kuroe.Flows.Shared;
using Kuroe.Shared;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>错误的统一输出与按错误码给出的可操作指引。</summary>
public sealed class ErrorViewTests
{
    [Fact]
    public void Report_writes_code_and_description_to_the_error_stream()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out TestConsole error);
        ErrorView errors = new(terminal);

        errors.Report([Error.Failure("Demo.Code", "出问题了")]);

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("错误：Demo.Code：出问题了", error.Output);
    }

    [Theory]
    [InlineData(ErrorCodes.SettingsModelPath, "模型选择用 /model <模型> 切换，/model none 取消选择。")]
    [InlineData(ErrorCodes.SettingsNullValue, "清除设置用 /unset <路径>。")]
    [InlineData(FlowErrorCodes.TaskNotFound, "用 /task list 查看任务号。")]
    [InlineData(FlowErrorCodes.RunNotFound, "run 号全局递增，用 /task list 或 /task show <任务号> 查看。")]
    [InlineData(FlowErrorCodes.FlowNotFound, "用 /flow list 查看流程名。")]
    [InlineData(FlowErrorCodes.FlowInvalidPath, "/flow add 的文件参数相对工作目录解析，路径非法时换一个写法。")]
    [InlineData(FlowErrorCodes.FlowNode, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完用 /flow list 确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowBody, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完用 /flow list 确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowName, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完用 /flow list 确认是否加载成功。")]
    [InlineData(FlowErrorCodes.FlowFormat, "流程文件是工作目录 `.kuroe/` 下的 flows.json，改完用 /flow list 确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolDuplicate, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完用 /tool list 确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolInvalid, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完用 /tool list 确认是否加载成功。")]
    [InlineData(ErrorCodes.ToolFormat, "命令工具文件是工作目录 `.kuroe/` 下的 tools.json，改完用 /tool list 确认是否加载成功。")]
    public void Guide_maps_error_codes_to_hints(string code, string hint)
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure(code, "说明")]);

        Assert.Contains(hint, output.Output);
    }

    [Fact]
    public void Guide_without_a_mapped_code_prints_nothing()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.Guide([Error.Failure("Demo.Unmapped", "说明")]);

        Assert.Equal(string.Empty, output.Output);
    }

    [Fact]
    public void Guide_without_a_model_choice_points_to_selection()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(harness.Models, null);

        Assert.Contains("用 /model <模型> 选择模型", output.Output);
    }

    [Fact]
    public void Guide_for_an_unregistered_model_names_the_registration_command()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(harness.Models, "未注册");

        Assert.Contains("用 /model add 未注册 <提供商> 注册该模型。", output.Output);
    }

    [Fact]
    public void Guide_skips_blank_and_registered_models()
    {
        using KuroeHarness harness = KuroeHarness.Create();
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        ErrorView errors = new(terminal);

        errors.GuideModelRegistration(harness.Models, "   ");
        errors.GuideModelRegistration(harness.Models, "fake");

        Assert.Equal(string.Empty, output.Output);
    }
}
