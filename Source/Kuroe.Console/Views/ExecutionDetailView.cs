using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Ports;
using Kuroe.Node.Llm;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>执行详情：头部字段、输入取值及其出处、契约端口与过程记录。</summary>
internal sealed class ExecutionDetailView(Terminal terminal)
{
    /// <summary>取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task, ExecutionSnapshot execution)
    {
        ExecutionInput input = execution.Input;
        List<(string Label, IRenderable Value)> fields =
        [
            ("任务", new Text($"{task.Id} {task.Title}")),
            ("节点", new Text($"{task.OrdinalOf(input.Node)}/{task.TotalExecutableNodes}")),
            ("轮次", new Text($"第 {input.ExecutionCount} 轮")),
            ("请求", new Text(ViewLabels.Requests(execution.Usage), Styles.Hint)),
            ("模型", new Text((input.Declaration as KuroeNodeDeclaration)?.Model.Value ?? "(未声明)")),
            .. ViewLabels.TokenEntries(execution.Usage)
                .Select(entry => (entry.Label, (IRenderable)new Text(entry.Value, Styles.Hint))),
            ("上下文", new Text(ViewLabels.Context(execution.Usage), Styles.Hint)),
            ("状态", new Text(ViewLabels.State(execution), ViewLabels.StyleOf(execution.State))),
            ("恢复数据", new Text(execution.HasRestoreData ? "有" : "无", Styles.Hint)),
            ("继续自", new Text(execution.ContinuedFrom?.Value.ToString() ?? "—", Styles.Hint)),
            ("开始", new Text(ViewLabels.Clock(execution.StartedAt), Styles.Hint)),
            ("结束", new Text(ViewLabels.Clock(execution.FinishedAt), Styles.Hint)),
            ("耗时", new Text(ViewLabels.Elapsed(execution.Elapsed), Styles.Hint)),
        ];

        _terminal.Write(Blocks.Section(BoxKind.View, "概要", Blocks.Fields([.. fields])));

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

        if (input.Declaration is KuroeNodeDeclaration)
        {
            IReadOnlyList<Port> ports = input.RequiredPorts;
            _terminal.NewLine();
            _terminal.Write(Blocks.Section(
                BoxKind.View,
                $"契约端口 {ports.Count} 个",
                ports.Count == 0
                    ? new Text("本次执行没有需要交回的端口。", Styles.Hint)
                    : new Text(PortRules.List(ports))));
        }

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(
            BoxKind.View,
            "过程记录",
            execution.Journal.Count == 0 && !execution.State.IsLive()
                ? new Text("过程记录只在运行中可见。", Styles.Hint)
                : JournalView.Build(execution.Journal)));
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
