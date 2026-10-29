using Kuroe.Console.Views;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests;

/// <summary>过程记录表格的渲染。</summary>
public sealed class JournalViewTests
{
    [Fact]
    public void Entry_renders_time_label_and_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry entry = new(DateTimeOffset.Now, JournalLevel.Information, "回复", "内容");

        terminal.Write(JournalView.Build([entry], 0));

        Assert.Contains(ViewLabels.Clock(entry.At), output.Output);
        Assert.Contains("回复", output.Output);
        Assert.Contains("内容", output.Output);
    }

    [Fact]
    public void Multiline_text_renders_each_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry entry = new(
            DateTimeOffset.Now,
            JournalLevel.Warning,
            "工具",
            "file/ReadFile {}\n失败 不存在");

        terminal.Write(JournalView.Build([entry], 0));

        Assert.Contains("file/ReadFile {}", output.Output);
        Assert.Contains("失败 不存在", output.Output);
    }

    [Fact]
    public void Dropped_and_limited_entries_are_reported()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry[] entries = [.. Enumerable.Range(1, 45).Select(index =>
            new JournalEntry(DateTimeOffset.Now, JournalLevel.Information, "信息", index.ToString()))];

        terminal.Write(JournalView.Build(entries, 2));

        Assert.Contains("更早的 2 条记录已丢弃。", output.Output);
        Assert.Contains("只呈现最近 40 条记录。", output.Output);
    }

    [Fact]
    public void Empty_journal_reports_no_entries()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);

        terminal.Write(JournalView.Build([], 0));

        Assert.Contains("还没有记录。", output.Output);
    }
}