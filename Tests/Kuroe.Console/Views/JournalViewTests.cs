using Kuroe.Console.Views;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Spectre.Console.Testing;
using Xunit;

namespace Kuroe.Console.Tests.Views;

/// <summary>过程记录表格的渲染。</summary>
public sealed class JournalViewTests
{
    /// <summary>固定的记录时间。</summary>
    private static readonly DateTimeOffset At = new(2026, 1, 2, 15, 4, 5, TimeSpan.FromHours(8));

    [Fact]
    public void Entry_renders_label_and_text()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry entry = new(At, JournalLevel.Information, "回复", "内容");

        terminal.Write(JournalView.Build([entry], 0));

        Assert.Contains("回复", output.Output);
        Assert.Contains("内容", output.Output);
    }

    [Fact]
    public void Multiline_text_renders_each_line()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry entry = new(
            At,
            JournalLevel.Warning,
            "工具",
            "file/ReadFile {}\n不存在");

        terminal.Write(JournalView.Build([entry], 0));

        Assert.Contains("file/ReadFile {}", output.Output);
        Assert.Contains("不存在", output.Output);
        Assert.DoesNotContain("失败", output.Output);
    }

    [Fact]
    public void Dropped_and_limited_entries_are_reported()
    {
        Terminal terminal = ConsoleSession.NewTerminal(out TestConsole output, out _);
        JournalEntry[] entries = [.. Enumerable.Range(1, 45).Select(index =>
            new JournalEntry(At, JournalLevel.Information, "信息", index.ToString()))];

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