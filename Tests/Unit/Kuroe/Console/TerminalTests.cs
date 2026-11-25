using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>终端的写出口：独占期间的通知排队、页面标题重绘与两个方向的流分离。</summary>
public sealed class TerminalTests
{
    [Fact(DisplayName = "独占期间通知排队之后刷出")]
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

    [Fact(DisplayName = "页面渲染带标题的通栏分隔线")]
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

    [Fact(DisplayName = "页面拼接路径段并转义")]
    public void Page_joins_path_segments_and_escapes_them()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("主菜单", "任务 [1]");

        Assert.Contains("主菜单 › 任务 [1]", output.Output);
    }

    [Fact(DisplayName = "页面把标题作为纯文本")]
    public void Page_treats_the_title_as_plain_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;

        terminal.Page("任务 [1]");

        Assert.Contains("任务 [1]", output.Output);
    }

    [Fact(DisplayName = "页面截断放不下的标题")]
    public void Page_truncates_a_title_that_does_not_fit()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 10;

        terminal.Page("任务 #1 一个很长的标题");

        string line = Assert.Single(Rows(output.Output));
        Assert.Equal(10, line.GetCellWidth());
        Assert.Contains('…', line);
    }

    [Fact(DisplayName = "页面使用强调色并加粗标题")]
    public void Page_uses_the_accent_color_and_bolds_the_title()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);
        output.Profile.Width = 40;

        terminal.Page("主菜单", "任务");

        Assert.Contains("\u001b[1;38;5;14m", output.Output);
        Assert.Empty(error.Output);
    }

    [Fact(DisplayName = "页面保留横幅与首屏")]
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

    [Fact(DisplayName = "首个标题之后每个标题前清屏并清除回滚")]
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

    [Fact(DisplayName = "页面每次重绘都清屏")]
    public void Page_clears_on_every_redraw()
    {
        Terminal terminal = NewAnsiTerminal(out TestConsole output, out TestConsole error);

        terminal.Page("主菜单");
        terminal.Page("主菜单");
        terminal.Page("主菜单");

        Assert.Equal(2, output.Output.Split("\u001b[2J", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("\u001b[2J", error.Output);
    }

    [Fact(DisplayName = "不支持 ANSI 时不写清屏序列")]
    public void Page_writes_no_clear_sequence_without_ansi_support()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);
        terminal.Page("主菜单");

        terminal.Page("主菜单");

        Assert.DoesNotContain('\u001b', output.Output);
        Assert.DoesNotContain('\u001b', error.Output);
        Assert.Equal(2, output.Output.Split("主菜单").Length - 1);
    }

    [Fact(DisplayName = "独占不能重复获取")]
    public void Exclusive_cannot_be_taken_twice()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out _, out _);

        using IDisposable first = terminal.Exclusive();

        Assert.Throws<InvalidOperationException>(() => terminal.Exclusive());
    }

    [Fact(DisplayName = "错误与提示进入错误流")]
    public void Error_and_note_go_to_the_error_stream()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out TestConsole error);

        terminal.Error("失败说明");
        terminal.Note("未生效。");

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("失败说明", error.Output);
        Assert.Contains("未生效。", error.Output);
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
