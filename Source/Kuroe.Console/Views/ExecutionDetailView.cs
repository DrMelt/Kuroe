using Kuroe.Flow.Execution;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>执行详情：头部字段、输入取值及其出处、过程记录。</summary>
internal sealed class ExecutionDetailView(Terminal terminal)
{
    /// <summary>取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task, ExecutionSnapshot execution)
    {
        TimeSpan elapsed = (execution.FinishedAt ?? DateTimeOffset.UtcNow) - (execution.StartedAt ?? execution.CreatedAt);
        List<(string Label, IRenderable Value)> fields =
        [
            ("任务", new Text($"{task.Id} {task.Title}")),
            ("节点", new Text(execution.Request.Target.Value)),
            ("轮次", new Text($"第 {execution.Request.Attempt} 轮")),
            ("请求", new Text(ViewLabels.Requests(execution.Usage), Styles.Hint)),
            .. ViewLabels.TokenEntries(execution.Usage)
                .Select(entry => (entry.Label, (IRenderable)new Text(entry.Value, Styles.Hint))),
            ("上下文", new Text(ViewLabels.Context(execution.Usage), Styles.Hint)),
            ("状态", new Text(execution.State.ToString(), execution.State switch
            {
                Kuroe.Flow.Execution.ExecutionState.Succeeded => Styles.Success,
                Kuroe.Flow.Execution.ExecutionState.Failed => Styles.Error,
                Kuroe.Flow.Execution.ExecutionState.Running or Kuroe.Flow.Execution.ExecutionState.Queued => Styles.Key,
                _ => Styles.Hint,
            })),
            ("恢复数据", new Text(execution.RestoreData is null ? "无" : "有", Styles.Hint)),
            ("继续自", new Text(execution.ContinuedFrom?.Value.ToString() ?? "—", Styles.Hint)),
            ("开始", new Text(ViewLabels.Clock(execution.StartedAt), Styles.Hint)),
            ("结束", new Text(ViewLabels.Clock(execution.FinishedAt), Styles.Hint)),
            ("耗时", new Text(ViewLabels.Elapsed(elapsed), Styles.Hint)),
        ];

        _terminal.Write(Blocks.Section(BoxKind.View, "概要", Blocks.Fields([.. fields])));

        foreach (string failure in execution.Failures)
        {
            _terminal.Failure($"失败：{failure}");
        }

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            $"输入取值 {execution.Request.Inputs.Count} 条",
            execution.Request.Inputs.Count == 0
                ? new Text("没有取到任何来源内容。", Styles.Hint)
                : InputTable(execution.Request.Inputs)));

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            "过程记录",
            execution.Journal.Count == 0 && !execution.State.IsLive()
                ? new Text("过程记录只在运行中可见。", Styles.Hint)
                : JournalView.Build(execution.Journal)));
    }

    /// <summary>输入取值按出处分组，内容一行内截断。</summary>
    private static Table InputTable(IReadOnlyList<ExecutionInputValue> inputs)
    {
        Table table = Blocks.Table(("来源 · 端口", false), ("内容", true));
        foreach (IGrouping<string?, ExecutionInputValue> group in inputs.GroupBy(value => value.Source))
        {
            foreach (ExecutionInputValue value in group)
            {
                table.AddRow(
                    new Text($"{group.Key ?? "—"} · {value.Port}", Styles.Key),
                    new Text(ViewLabels.OneLine(value.Text, ValueLimit)));
            }
        }

        return table;
    }
}