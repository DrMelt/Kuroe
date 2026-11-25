using Kuroe.Kernel.Tasks.Presentation.Shared;
using Spectre.Console;
using Spectre.Console.Rendering;
using Kuroe.Kernel.Execution.Shared.Ports;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Console.Views;

/// <summary>执行详情：头部字段、输入取值及其出处、契约端口与过程记录。</summary>
internal sealed class ExecutionDetailView(Terminal terminal, TimeProvider time)
{
    /// <summary>取值内容保留的字符数。</summary>
    private const int ValueLimit = 70;

    private readonly Terminal _terminal = terminal;
    private readonly TimeProvider _time = time;

    public void Print(TaskSnapshot task, ExecutionSnapshot execution)
    {
        TimeSpan elapsed = (execution.FinishedAt ?? _time.GetUtcNow()) - (execution.StartedAt ?? execution.CreatedAt);
        List<(string Label, IRenderable Value)> fields =
        [
            ("任务", new Text($"{task.Id} {task.Title}")),
            ("步骤", new Text(execution.Request.Step.Value)),
            ("轮次", new Text($"第 {task.RoundOf(execution)} 轮")),
            ("请求", new Text(ViewLabels.Requests(execution.Usage), Styles.Hint)),
            .. ViewLabels.TokenEntries(execution.Usage)
                .Select(entry => (entry.Label, (IRenderable)new Text(entry.Value, Styles.Hint))),
            ("上下文", new Text(ViewLabels.Context(execution.Usage), Styles.Hint)),
            ("状态", new Text(execution.State.ToString(), ViewLabels.StyleOf(execution.State))),
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

        IReadOnlyList<ExecutionOutputPort> ports =
            [.. execution.Request.OutputPorts.Where(port => port.Required)];
        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            $"契约端口 {ports.Count} 个",
            ports.Count == 0
                ? new Text("本次执行没有需要交回的端口。", Styles.Hint)
                : PortTable(ports)));

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            "过程记录",
            execution.Journal.Count == 0 && !execution.State.IsLive()
                ? new Text("过程记录只在运行中可见。", Styles.Hint)
                : JournalView.Build(execution.Journal)));
    }

    /// <summary>输入取值按出处分组，来源与目标输入端口分列，内容一行内截断。</summary>
    private static Table InputTable(IReadOnlyList<ExecutionInputValue> inputs)
    {
        Table table = Blocks.Table(("来源", false), ("端口", false), ("内容", true));
        foreach (IGrouping<string?, ExecutionInputValue> group in inputs.GroupBy(value => value.Source))
        {
            foreach (ExecutionInputValue value in group)
            {
                (string text, bool truncated) = ViewLabels.Truncate(value.Text, ValueLimit);
                Paragraph content = new(text);
                if (truncated)
                {
                    content.Append("（已截断）", Styles.Hint);
                }

                table.AddRow(
                    new Text(group.Key ?? "—", Styles.Key),
                    new Text(value.Port.Value, Styles.Key),
                    content);
            }
        }

        return table;
    }

    /// <summary>契约端口按端口、类型、模式与说明分列，说明列换行。</summary>
    private static Table PortTable(IReadOnlyList<ExecutionOutputPort> ports)
    {
        Table table = Blocks.Table(("端口", false), ("类型", false), ("模式", false), ("说明", true));
        foreach (ExecutionOutputPort port in ports)
        {
            table.AddRow(
                new Text(port.Name.Value, Styles.Key),
                new Text(port.Kind.ToString(), Styles.Hint),
                new Text(port.CarriesTrigger ? "Trigger" : "Plain", Styles.Hint),
                new Text(port.Description ?? string.Empty, Styles.Hint));
        }

        return table;
    }
}