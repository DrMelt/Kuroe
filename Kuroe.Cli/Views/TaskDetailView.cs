using Kuroe.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务详情：按叶子列出已执行与在执行的 agent，再列出单元结论与前台对话。</summary>
internal sealed class TaskDetailView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task)
    {
        _terminal.Line($"{task.Id} · {task.Title}　状态：{Labels.Of(task.State)}");
        _terminal.Line($"目标：{task.Goal}");
        _terminal.Line($"流程：{task.Flow.Name}（{string.Join(" → ", task.Graph.Leaves.Select(leaf => leaf.Path))}）"
            + $"　节点进度 {task.FrontierNodes}/{task.TotalNodes}　前台对话 {task.DialogueTurns} 回合");

        if (task.Splits.Count > 0)
        {
            _terminal.NewLine();
            foreach ((int leafIndex, PlanOutput plan) in task.Splits.OrderBy(entry => entry.Key))
            {
                string nodeName = leafIndex < task.Graph.Count ? task.Graph[leafIndex].Name : $"叶子 {leafIndex + 1}";
                _terminal.Line($"拆分由 {plan.Origin} 在节点「{nodeName}」交回：");
                foreach (PlanItem item in plan.Items)
                {
                    UnitSnapshot? unit = task.Units.FirstOrDefault(candidate => candidate.ItemIndex == item.Index);
                    string verdict = unit is null ? string.Empty : $"　{Labels.Of(unit.Verdict)}";
                    string branch = item.Branch is { Length: > 0 } name ? $"（{name}）" : string.Empty;
                    _terminal.Line($"  {item.Index + 1}. {item.Title}{branch}{verdict}");
                }
            }
        }

        PrintUnits(task);

        _terminal.NewLine();
        foreach (NodeSnapshot node in task.Nodes)
        {
            _terminal.ToolCall($"叶子 {node.Index + 1} · {node.Leaf.Path}"
                + $"（{node.Leaf.Output.Label()} · {Labels.Of(node.Leaf.Mode)} · {Labels.Of(node.Leaf.Gate)}）");

            if (node.Runs.Count == 0)
            {
                _terminal.Hint("  还没有 agent。");
                continue;
            }

            foreach (RunSnapshot run in node.Runs)
            {
                _terminal.Line($"  {AgentLabel(run)}");
            }
        }

        foreach (UnitSnapshot unit in task.Units.Where(unit => unit.Findings is { Length: > 0 }))
        {
            _terminal.Warn($"  {Labels.Item(unit.ItemIndex)} 的检查意见：{unit.Findings}");
        }

        _terminal.NewLine();
        _terminal.Line("前台对话：");
        JournalPrinter.Print(_terminal, task.Dialogue, task.DroppedDialogue);
    }

    /// <summary>执行单元推进到哪片叶子、结论如何。</summary>
    private void PrintUnits(TaskSnapshot task)
    {
        if (task.Units.Count == 0)
        {
            return;
        }

        _terminal.NewLine();
        _terminal.Line("单元推进：");
        foreach (UnitSnapshot unit in task.Units)
        {
            string verdict = unit.Verdict == UnitVerdict.NotChecked
                ? string.Empty
                : $" · {Labels.Of(unit.Verdict)} · 检查 {unit.Attempts} 轮";
            _terminal.Line($"  {Labels.Item(unit.ItemIndex)} · 下一步 {NodeName(task, unit.NodeCursor)}"
                + $" · {Labels.Of(unit.State)}{verdict}");
        }
    }

    private static string NodeName(TaskSnapshot task, int cursor) =>
        cursor >= task.TotalNodes ? "结束" : task.Graph[cursor].Name;

    /// <summary>列表里一行的 agent 概况。</summary>
    internal static string AgentLabel(RunSnapshot run)
    {
        List<string> parts =
        [
            run.Id.ToString(),
            Labels.Item(run.Context.ItemIndex),
            $"第 {run.Context.Attempt} 轮",
            Labels.State(run),
            Labels.Elapsed(run.Elapsed),
        ];

        return string.Join(" · ", parts);
    }
}
