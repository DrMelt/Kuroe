using Kuroe.Node.Llm;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;

namespace Kuroe.Cli.Views;

/// <summary>执行详情：头部、输入取值及其出处、逐步过程。</summary>
internal sealed class ExecutionDetailView(Terminal terminal)
{
    /// <summary>取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task, ExecutionSnapshot execution)
    {
        ExecutionInput input = execution.Input;
        _terminal.Line($"{execution.Id} · {input.Node} · 第 {input.ExecutionCount} 轮");
        _terminal.Line($"{task.Id} {task.Title}\u3000节点 {task.OrdinalOf(input.Node)}/{task.TotalExecutableNodes}\u3000模型 {(input.Declaration as KuroeNodeDeclaration)?.Model.Value ?? "(未声明)"}");
        _terminal.Line($"状态 {ViewLabels.State(execution)}\u3000{ViewLabels.Clock(execution.StartedAt)} → {ViewLabels.Clock(execution.FinishedAt)}\u3000耗时 {ViewLabels.Elapsed(execution.Elapsed)}");

        foreach (string failure in execution.Failures)
        {
            _terminal.Failure($"失败：{failure}");
        }

        _terminal.NewLine();
        _terminal.Line($"输入取值（{input.Inputs.Count} 条）：");
        if (input.Inputs.Count == 0)
        {
            _terminal.Hint("  没有取到任何来源内容。");
        }

        foreach (ContextValue value in input.Inputs)
        {
            _terminal.Line($"  {value.Origin.Label}");
            _terminal.Line($"    {OneLine(value.Text)}");
        }

        _terminal.NewLine();
        _terminal.Line("过程：");
        JournalView.Print(_terminal, execution.Journal, execution.DroppedEntries);
    }

    private static string OneLine(string text)
    {
        string flat = text.Replace('\n', ' ').Replace('\r', ' ');

        return flat.Length <= ValueLimit ? flat : string.Concat(flat.AsSpan(0, ValueLimit), "…");
    }
}
