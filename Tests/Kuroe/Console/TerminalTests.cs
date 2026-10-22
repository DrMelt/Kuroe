using Kuroe.Console.Views;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>终端的写出口：独占期间的通知排队、行闭合、两个方向的流分离与框内流式排版。</summary>
public sealed class TerminalTests
{
    [Fact]
    public void Notice_queues_while_exclusive_and_flushes_after()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        using (terminal.Exclusive())
        {
            terminal.Notice("排队中的提示。", Styles.Warning);
            Assert.DoesNotContain("排队中的提示。", output.Output);
        }

        Assert.Contains("排队中的提示。", output.Output);
    }

    [Fact]
    public void Line_closes_a_dangling_append_first()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Append("流式片段");
        terminal.Line("下一行");

        Assert.Contains($"流式片段{Environment.NewLine}下一行", output.Output);
    }

    [Fact]
    public void Page_renders_a_full_width_rule_with_the_title()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("任务列表 › 任务 #1 目标");

        string line = Assert.Single(Rows(output.Output));
        Assert.StartsWith("── 任务列表 › 任务 #1 目标 ", line);
        Assert.EndsWith("─", line);
        Assert.Equal(40, line.GetCellWidth());
    }

    [Fact]
    public void Page_treats_the_title_as_plain_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("任务 [1]");

        Assert.Contains("任务 [1]", output.Output);
    }

    [Fact]
    public void Page_truncates_a_title_that_does_not_fit()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 10;

        terminal.Page("任务 #1 一个很长的标题");

        string line = Assert.Single(Rows(output.Output));
        Assert.Equal(10, line.GetCellWidth());
        Assert.Contains('…', line);
    }

    [Fact]
    public void Exclusive_cannot_be_taken_twice()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out _, out _);

        using IDisposable first = terminal.Exclusive();

        Assert.Throws<InvalidOperationException>(() => terminal.Exclusive());
    }

    [Fact]
    public void Error_and_note_go_to_the_error_stream()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);

        terminal.Error("失败说明");
        terminal.Note("未生效。");

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("失败说明", error.Output);
        Assert.Contains("未生效。", error.Output);
    }

    [Fact]
    public void Frame_wraps_streamed_text_between_borders()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 30;

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            terminal.Append(new string('字', 40));
        }

        string[] lines = Rows(output.Output);
        Assert.StartsWith("┌─Kuroe 助手", lines[0]);
        Assert.EndsWith("┐", lines[0]);
        Assert.Equal(30, lines[0].GetCellWidth());
        Assert.StartsWith("└", lines[^1]);
        Assert.EndsWith("┘", lines[^1]);
        Assert.Equal(30, lines[^1].GetCellWidth());

        string[] content = lines[1..^1];
        Assert.Equal(4, content.Length);
        foreach (string line in content)
        {
            Assert.StartsWith("│ ", line);
            Assert.EndsWith(" │", line);
            Assert.Equal(30, line.GetCellWidth());
        }
    }

    [Fact]
    public void Frame_puts_tool_lines_inside_the_box()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            terminal.Append("我先看一下。");
            terminal.ToolCall("工具 GetStatus {}");
            terminal.ToolResult("结果 执行中");
        }

        string[] lines = Rows(output.Output);
        Assert.Contains(lines, line => line.Contains("│ 我先看一下。") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 工具 GetStatus {}") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 结果 执行中") && line.EndsWith('│'));
    }

    [Fact]
    public void Frame_closes_when_the_scope_throws()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 30;

        Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
            {
                terminal.Append("已取消。");
                throw new InvalidOperationException("失败");
            }
        }));

        string[] lines = Rows(output.Output);
        Assert.StartsWith("└", lines[^1]);
        Assert.EndsWith("┘", lines[^1]);
    }

    [Fact]
    public void Frame_falls_back_to_plain_lines_on_a_narrow_console()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 10;

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            terminal.Append("短文本");
        }

        Assert.DoesNotContain('┌', output.Output);
        Assert.DoesNotContain('│', output.Output);
        Assert.Contains("短文本", output.Output);
    }

    [Fact]
    public void Frame_keeps_surrogate_pairs_together()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 20;

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            terminal.Append(string.Concat(Enumerable.Repeat("😀", 10)));
        }

        string[] content = Rows(output.Output)[1..^1];
        Assert.True(content.Length >= 2, output.Output);
        Assert.Equal(10, content.Sum(line => line.Split("😀").Length - 1));
    }

    [Fact]
    public void Frame_rejects_renderables()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out _, out _);

        using Terminal.Frame frame = terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手");

        Assert.Throws<InvalidOperationException>(() => terminal.Write(new Text("内容")));
    }

    private static string[] Rows(string output) =>
        output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
