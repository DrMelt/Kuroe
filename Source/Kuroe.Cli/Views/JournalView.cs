using Kuroe.Flow.Shared.Execution.Process;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Cli.Views;

/// <summary>过程记录的表格呈现，任务与执行详情共用。一条调用占两行：调用与结果。</summary>
internal static class JournalView
{
    /// <summary>最多呈现的记录数，更早的只报条数。</summary>
    private const int Limit = 40;

    public static IRenderable Build(IReadOnlyList<JournalEntry> entries, int dropped)
    {
        List<IRenderable> lines = [];
        if (dropped > 0)
        {
            lines.Add(new Text($"更早的 {dropped} 条记录已丢弃。", Styles.Hint));
        }

        List<JournalEntry> shown = [.. entries.Skip(Math.Max(0, entries.Count - Limit))];
        if (entries.Count > shown.Count)
        {
            lines.Add(new Text($"只呈现最近 {shown.Count} 条记录。", Styles.Hint));
        }

        if (shown.Count == 0)
        {
            lines.Add(new Text("还没有记录。", Styles.Hint));

            return new Rows(lines);
        }

        Table table = Blocks.Table(("时间", false), ("类型", false), ("内容", true));
        foreach (JournalEntry entry in shown)
        {
            Add(table, entry);
        }

        lines.Add(table);

        return new Rows(lines);
    }

    private static void Add(Table table, JournalEntry entry)
    {
        switch (entry)
        {
            case PromptEntry prompt:
                table.AddRow(
                    new Text(ViewLabels.Clock(entry.At), Styles.Hint),
                    new Text("输入", Styles.Key),
                    new Text(prompt.Text, Styles.Key));
                break;

            case TextEntry text:
                table.AddRow(
                    new Text(ViewLabels.Clock(entry.At), Styles.Hint),
                    new Text("回复", Styles.Hint),
                    new Text(text.Text));
                break;

            case ToolCallEntry call:
                string invocation = call.Call.Arguments.Length == 0
                    ? call.Call.Name
                    : $"{call.Call.Name} {call.Call.Arguments}";
                table.AddRow(
                    new Text(ViewLabels.Clock(entry.At), Styles.Hint),
                    new Text("工具", Styles.Key),
                    new Text(invocation, Styles.Key));
                table.AddRow(
                    new Text(string.Empty),
                    new Text(call.Call.Failed ? "失败" : "结果", call.Call.Failed ? Styles.Warning : Styles.Hint),
                    new Text(call.Call.Outcome, call.Call.Failed ? Styles.Warning : Styles.Hint));
                break;

            case ErrorEntry error:
                table.AddRow(
                    new Text(ViewLabels.Clock(entry.At), Styles.Hint),
                    new Text("错误", Styles.Error),
                    new Text(error.Text, Styles.Error));
                break;

            case DiscardedEntry:
                table.AddRow(
                    new Text(ViewLabels.Clock(entry.At), Styles.Hint),
                    new Text("丢弃", Styles.Warning),
                    new Text("本轮未计入上下文。", Styles.Warning));
                break;
        }
    }
}
