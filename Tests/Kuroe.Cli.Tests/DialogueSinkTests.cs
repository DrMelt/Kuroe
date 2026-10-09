using Kuroe.Cli.Views;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Tools;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>对话回合终端的工具调用行与失败行呈现。</summary>
public sealed class DialogueSinkTests
{
    [Fact]
    public void OnToolCall_success_prints_tool_result_line()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        DialogueSink sink = new(terminal);

        sink.OnToolCall(new ToolCallRecord(new ToolName("WriteFile"), """{"path":"a.txt"}""", "已写入 5 字符。", false));

        Assert.Contains($"工具 > {Environment.NewLine}WriteFile {{\"path\":\"a.txt\"}}", output.Output);
        Assert.Contains($"结果 > {Environment.NewLine}已写入 5 字符。", output.Output);
        Assert.DoesNotContain("失败 >", output.Output);
    }

    [Fact]
    public void OnToolCall_failure_prints_warning_line()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        DialogueSink sink = new(terminal);

        sink.OnToolCall(new ToolCallRecord(new ToolName("WriteFile"), """{"path":"a.txt"}""", "被拒绝：缺少参数。", true));

        Assert.Contains($"失败 > {Environment.NewLine}被拒绝：缺少参数。", output.Output);
        Assert.DoesNotContain("结果 >", output.Output);
    }

    [Fact]
    public void OnToolCall_after_text_starts_on_new_line()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);
        DialogueSink sink = new(terminal);

        sink.OnText("我来查看一下。");
        sink.OnToolCall(new ToolCallRecord(new ToolName("ListTools"), "{}", "内置对话可用函数与分组", false));

        Assert.Contains($"我来查看一下。{Environment.NewLine}工具 > {Environment.NewLine}ListTools {{}}", output.Output);
    }
}
