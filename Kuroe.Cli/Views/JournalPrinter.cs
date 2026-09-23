using Kuroe.Agent.Turns;

namespace Kuroe.Cli.Views;

/// <summary>过程记录的逐行呈现，前台对话与 agent 详情共用。</summary>
internal static class JournalPrinter
{
    /// <summary>最多呈现的记录数，更早的只报条数。</summary>
    private const int Limit = 40;

    public static void Print(Terminal terminal, IReadOnlyList<JournalEntry> entries, int dropped)
    {
        if (dropped > 0)
        {
            terminal.Hint($"更早的 {dropped} 条记录已丢弃。");
        }

        List<JournalEntry> shown = [.. entries.Skip(Math.Max(0, entries.Count - Limit))];
        if (entries.Count > shown.Count)
        {
            terminal.Hint($"只呈现最近 {shown.Count} 条记录。");
        }

        if (shown.Count == 0)
        {
            terminal.Hint("还没有记录。");
            return;
        }

        foreach (JournalEntry entry in shown)
        {
            string at = Labels.Clock(entry.At);
            switch (entry)
            {
                case PromptEntry prompt:
                    terminal.ToolCall($"[{at}] 输入 > {prompt.Text}");
                    break;

                case TextEntry text:
                    terminal.Line($"[{at}] {text.Text}");
                    break;

                case ToolCallEntry call:
                    terminal.ToolCall($"[{at}] 工具 > {call.Call.Name} {call.Call.Arguments}");
                    terminal.ToolResult($"        结果 > {call.Call.Outcome}");
                    break;

                case ErrorEntry error:
                    terminal.Failure($"[{at}] 错误 > {error.Text}");
                    break;

                case DiscardedEntry:
                    terminal.Warn($"[{at}] 本轮未计入上下文。");
                    break;
            }
        }
    }
}
