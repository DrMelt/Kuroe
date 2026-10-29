using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>执行详情：头部字段、输入取值及其出处、过程记录。</summary>
internal sealed class ExecutionDetailView(Terminal terminal)
{
    /// <summary>取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task, ExecutionSnapshot execution)
    {
        ExecutionInput input = execution.Input;
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            "概要",
            Blocks.Fields(
                ("任务", new Text($"{task.Id} {task.Title}")),
                ("节点", new Text($"{task.OrdinalOf(input.Node)}/{task.TotalExecutableNodes}")),
                ("轮次", new Text($"第 {input.ExecutionCount} 轮")),
                ("模型", new Text((input.Declaration as KuroeNodeDeclaration)?.Model.Value ?? "(未声明)")),
                ("状态", new Text(ViewLabels.State(execution), ViewLabels.StyleOf(execution.State))),
                ("开始", new Text(ViewLabels.Clock(execution.StartedAt), Styles.Hint)),
                ("结束", new Text(ViewLabels.Clock(execution.FinishedAt), Styles.Hint)),
                ("耗时", new Text(ViewLabels.Elapsed(execution.Elapsed), Styles.Hint)))));

        foreach (string failure in execution.Failures)
        {
            _terminal.Failure($"失败：{failure}");
        }

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            $"输入取值 {input.Inputs.Count} 条",
            input.Inputs.Count == 0
                ? new Text("没有取到任何来源内容。", Styles.Hint)
                : InputTable(input)));

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(BoxKind.View, "过程记录", JournalView.Build(execution.Journal, execution.DroppedEntries)));
    }

    /// <summary>输入取值按出处分组，内容一行内截断。</summary>
    private static Table InputTable(ExecutionInput input)
    {
        Table table = Blocks.Table(("来源 · 端口", false), ("内容", true));
        foreach (IGrouping<ContextSource, InputValue> group in input.Inputs.GroupBy(value => value.Origin))
        {
            foreach (InputValue value in group)
            {
                table.AddRow(
                    new Text($"{group.Key.Label} · {value.Port}", Styles.Key),
                    new Text(ViewLabels.OneLine(value.Text, ValueLimit)));
            }
        }

        return table;
    }
}
