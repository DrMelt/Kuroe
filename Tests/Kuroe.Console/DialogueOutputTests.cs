using Kuroe.Console.Views;
using Kuroe.Flow.Shared.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>对话终端的工具调用行与失败行呈现。</summary>
public sealed class DialogueOutputTests
{
    [Fact]
    public void ToolCall_success_prints_tool_result_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.ToolCall(new ToolCallRecord("WriteFile", """{"path":"a.txt"}""", "已写入 5 字符。", false));

        Assert.Contains($"工具 WriteFile {{\"path\":\"a.txt\"}}", output.Output);
        Assert.Contains("结果 已写入 5 字符。", output.Output);
        Assert.DoesNotContain("失败 ", output.Output);
    }

    [Fact]
    public void ToolCall_failure_prints_warning_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.ToolCall(new ToolCallRecord("WriteFile", """{"path":"a.txt"}""", "被拒绝：缺少参数。", true));

        Assert.Contains("失败 被拒绝：缺少参数。", output.Output);
        Assert.DoesNotContain("结果 ", output.Output);
    }

    [Fact]
    public void ToolCall_after_text_starts_on_new_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Text("我来查看一下。");
        dialogue.ToolCall(new ToolCallRecord("ListTools", "{}", "内置对话可用函数与分组", false));

        Assert.Contains($"我来查看一下。{Environment.NewLine}工具 ListTools {{}}", output.Output);
    }

    [Fact]
    public void ToolCall_inside_a_frame_stays_inside_the_borders()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;
        DialogueOutput dialogue = new(terminal);

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            dialogue.Text("我来查看一下。");
            dialogue.ToolCall(new ToolCallRecord("ListTools", "{}", "内置对话可用函数与分组", false));
        }

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, line => line.Contains("│ 我来查看一下。") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 工具 ListTools {}") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 结果 内置对话可用函数与分组") && line.EndsWith('│'));
    }
}
