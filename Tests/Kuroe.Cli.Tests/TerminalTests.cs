using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Cli.Tests;

/// <summary>终端的写出口：独占期间的通知排队、行闭合与两个方向的流分离。</summary>
public sealed class TerminalTests
{
    [Fact]
    public void Notice_queues_while_exclusive_and_flushes_after()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);

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
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out _);

        terminal.Append("流式片段");
        terminal.Line("下一行");

        Assert.Contains($"流式片段{Environment.NewLine}下一行", output.Output);
    }

    [Fact]
    public void Exclusive_cannot_be_taken_twice()
    {
        Terminal terminal = Ui.NewTerminal(out _, out _);

        using IDisposable first = terminal.Exclusive();

        Assert.Throws<InvalidOperationException>(() => terminal.Exclusive());
    }

    [Fact]
    public void Error_and_note_go_to_the_error_stream()
    {
        Terminal terminal = Ui.NewTerminal(out TestConsole output, out TestConsole error);

        terminal.Error("失败说明");
        terminal.Note("未生效。");

        Assert.Equal(string.Empty, output.Output);
        Assert.Contains("失败说明", error.Output);
        Assert.Contains("未生效。", error.Output);
    }
}
