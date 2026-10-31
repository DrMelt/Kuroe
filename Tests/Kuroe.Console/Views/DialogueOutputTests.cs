using Kuroe.Console.Views;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>对话终端的内容、工具调用行与失败行呈现。</summary>
public sealed class DialogueOutputTests
{
    [Fact]
    public void Entry_success_prints_tool_result_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Entry(new JournalEntry(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            "工具",
            "WriteFile {\"path\":\"a.txt\"}\n结果 已写入 5 字符。"));

        Assert.Contains($"工具 WriteFile {{\"path\":\"a.txt\"}}", output.Output);
        Assert.Contains("结果 已写入 5 字符。", output.Output);
        Assert.DoesNotContain("失败 ", output.Output);
    }

    [Fact]
    public void Entry_failure_prints_warning_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Entry(new JournalEntry(
            DateTimeOffset.UtcNow,
            JournalLevel.Warning,
            "工具",
            "WriteFile {\"path\":\"a.txt\"}\n失败 被拒绝：缺少参数。"));

        Assert.Contains("失败 被拒绝：缺少参数。", output.Output);
        Assert.DoesNotContain("结果 ", output.Output);
    }

    [Fact]
    public void Entry_after_text_starts_on_new_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Content(new ProcessContent(ProcessContentKind.Reply, "我来查看一下。"));
        dialogue.Entry(new JournalEntry(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            "工具",
            "ListTools {}\n结果 内置对话可用函数与分组"));

        Assert.Contains($"我来查看一下。{Environment.NewLine}工具 ListTools {{}}", output.Output);
    }

    [Fact]
    public void Reasoning_is_printed_with_its_prefix_before_the_reply()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "先想"));
        dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "再想"));
        dialogue.Content(new ProcessContent(ProcessContentKind.Reply, "答复"));

        Assert.Contains($"思考 先想再想{Environment.NewLine}答复", output.Output);
    }

    [Fact]
    public void Reasoning_after_reply_starts_on_a_new_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Content(new ProcessContent(ProcessContentKind.Reply, "答复"));
        dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "再想"));

        Assert.Contains($"答复{Environment.NewLine}思考 再想", output.Output);
    }

    [Fact]
    public void Entry_after_reasoning_starts_on_a_new_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        DialogueOutput dialogue = new(terminal);

        dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "先想"));
        dialogue.Entry(new JournalEntry(
            DateTimeOffset.UtcNow,
            JournalLevel.Information,
            "工具",
            "ListTools {}\n结果 内置对话可用函数与分组"));

        Assert.Contains($"思考 先想{Environment.NewLine}工具 ListTools {{}}", output.Output);
    }

    [Fact]
    public void Reasoning_uses_the_reasoning_style()
    {
        using TestConsole output = new();
        output.EmitAnsiSequences();
        output.Profile.Width = 40;
        Terminal terminal = new(output, output);
        DialogueOutput dialogue = new(terminal);

        dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "想"));

        Assert.Contains("\u001b[38;5;104m思考 ", output.Output);
    }

    [Fact]
    public void Reset_starts_the_next_round_without_previous_line_state()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;
        DialogueOutput dialogue = new(terminal);

        using (terminal.OpenFrame(BoxKind.Reply, "第一轮"))
        {
            dialogue.Content(new ProcessContent(ProcessContentKind.Reply, "第一轮回复"));
        }

        dialogue.Reset();

        using (terminal.OpenFrame(BoxKind.Reply, "第二轮"))
        {
            dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "想"));
        }

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        int top = Array.FindIndex(lines, line => line.Contains("第二轮"));
        Assert.Contains("│ 思考 想", lines[top + 1]);
    }

    [Fact]
    public void Entry_inside_a_frame_stays_inside_the_borders()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        output.Profile.Width = 40;
        DialogueOutput dialogue = new(terminal);

        using (terminal.OpenFrame(BoxKind.Reply, "Kuroe 助手"))
        {
            dialogue.Content(new ProcessContent(ProcessContentKind.Reasoning, "先想"));
            dialogue.Content(new ProcessContent(ProcessContentKind.Reply, "我来查看一下。"));
            dialogue.Entry(new JournalEntry(
                DateTimeOffset.UtcNow,
                JournalLevel.Information,
                "工具",
                "ListTools {}\n结果 内置对话可用函数与分组"));
        }

        string[] lines = output.Output.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains(lines, line => line.Contains("│ 思考 先想") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 我来查看一下。") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 工具 ListTools {}") && line.EndsWith('│'));
        Assert.Contains(lines, line => line.Contains("│ 结果 内置对话可用函数与分组") && line.EndsWith('│'));
    }
}
