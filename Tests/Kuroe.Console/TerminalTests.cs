using Kuroe.Console.Views;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>终端的写出口：独占期间的通知排队、行闭合、两个方向的流分离与框内文本排版。</summary>
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

        terminal.Append("片段");
        terminal.Line("下一行");

        Assert.Contains($"片段{Environment.NewLine}下一行", output.Output);
    }

    [Fact]
    public void Page_renders_a_full_width_rule_with_the_title()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("主菜单", "任务", "任务 #1 目标");

        string line = Assert.Single(Rows(output.Output));
        Assert.StartsWith("── 主菜单 › 任务 › 任务 #1 目标 ", line);
        Assert.EndsWith("─", line);
        Assert.Equal(40, line.GetCellWidth());
    }

    [Fact]
    public void Page_joins_path_segments_and_escapes_them()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("主菜单", "任务 [1]");

        Assert.Contains("主菜单 › 任务 [1]", output.Output);
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
    public void Page_uses_the_accent_color_and_bolds_the_title()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);
        output.Profile.Width = 40;

        terminal.Page("主菜单", "任务");

        Assert.Contains("\u001b[1;38;5;14m", output.Output);
        Assert.Empty(error.Output);
    }

    [Fact]
    public void Page_keeps_the_banner_and_the_first_screen()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);
        terminal.Line("Kuroe 已启动。");

        terminal.Page("主菜单");

        Assert.DoesNotContain("\u001b[2J", output.Output);
        Assert.DoesNotContain("\u001b[1;1H", output.Output);
        int banner = output.Output.IndexOf("Kuroe 已启动。", StringComparison.Ordinal);
        int title = output.Output.IndexOf("主菜单", StringComparison.Ordinal);
        Assert.True(banner >= 0, output.Output);
        Assert.True(title > banner, output.Output);
        Assert.DoesNotContain("\u001b[2J", error.Output);
    }

    [Fact]
    public void Page_clears_the_display_and_the_scrollback_before_every_title_after_the_first()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);
        terminal.Page("主菜单");

        terminal.Page("主菜单");

        int clear = output.Output.IndexOf("\u001b[2J\u001b[3J\u001b[1;1H", StringComparison.Ordinal);
        int title = output.Output.LastIndexOf("主菜单", StringComparison.Ordinal);
        Assert.True(clear >= 0, output.Output);
        Assert.True(title > clear, output.Output);
        Assert.DoesNotContain("\u001b[2J", error.Output);
    }

    [Fact]
    public void Page_clears_on_every_redraw()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);

        terminal.Page("主菜单");
        terminal.Page("主菜单");
        terminal.Page("主菜单");

        Assert.Equal(2, output.Output.Split("\u001b[2J", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("\u001b[2J", error.Output);
    }

    [Fact]
    public void Page_writes_no_clear_sequence_without_ansi_support()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);
        terminal.Page("主菜单");

        terminal.Page("主菜单");

        Assert.DoesNotContain('\u001b', output.Output);
        Assert.DoesNotContain('\u001b', error.Output);
        Assert.Equal(2, output.Output.Split("主菜单").Length - 1);
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

    [Fact]
    public void Page_rejects_a_page_inside_a_frame()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out _, out _);

        using Terminal.Frame frame = terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手");

        Assert.Throws<InvalidOperationException>(() => terminal.Page("主菜单"));
    }

    /// <summary>建一套控制序列原样写出的终端，两个方向各自捕获。</summary>
    private static Terminal NewAnsiTerminal(out TestConsole output, out TestConsole error)
    {
        output = NewAnsiConsole();
        error = NewAnsiConsole();

        return new Terminal(output, error);
    }

    private static TestConsole NewAnsiConsole()
    {
        TestConsole console = new();
        console.EmitAnsiSequences();

        return console;
    }

    private static string[] Rows(string output) =>
        output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
