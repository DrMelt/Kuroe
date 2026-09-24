using Kuroe.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务详情：按步骤列出已执行与在执行的 agent，再列出单元结论与前台对话。</summary>
internal sealed class TaskDetailView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task)
    {
        _terminal.Line($"{task.Id} · {task.Title}　状态：{Labels.Of(task.State)}");
        _terminal.Line($"目标：{task.Goal}");
        _terminal.Line($"流程：{task.Flow.Name}（{string.Join(" → ", task.Flow.Steps.Select(step => step.Name))}）"
            + $"　步骤进度 {task.FrontierSteps}/{task.TotalSteps}　前台对话 {task.DialogueTurns} 回合");

        if (task.Plan is { } plan)
        {
            _terminal.NewLine();
            _terminal.Line($"条目由 {plan.Origin} 交回：");
            foreach (PlanItem item in plan.Items)
            {
                UnitSnapshot? unit = task.Units.FirstOrDefault(candidate => candidate.ItemIndex == item.Index);
                string verdict = unit is null ? string.Empty : $"　{Labels.Of(unit.Verdict)}";
                _terminal.Line($"  {item.Index + 1}. {item.Title}{verdict}");
            }
        }

        PrintUnits(task);

        _terminal.NewLine();
        foreach (StepSnapshot step in task.Steps)
        {
            _terminal.ToolCall($"步骤 {step.Index + 1} · {step.Spec.Name}"
                + $"（{step.Spec.Role.Label()} · {Labels.Of(step.Spec.Scope)} · {Labels.Of(step.Spec.Gate)}）");

            if (step.Runs.Count == 0)
            {
                _terminal.Hint("  还没有 agent。");
                continue;
            }

            foreach (RunSnapshot run in step.Runs)
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

    /// <summary>执行单元推进到哪一步、结论如何。</summary>
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
            string verdict = unit.Verdict == UnitVerdict.NotChecked ? string.Empty : $" · {Labels.Of(unit.Verdict)}";
            _terminal.Line($"  {Labels.Item(unit.ItemIndex)} · 下一步 {StepName(task, unit.StepCursor)}"
                + $" · {Labels.Of(unit.State)}{verdict} · 第 {unit.Attempts} 轮");
        }
    }

    private static string StepName(TaskSnapshot task, int cursor) =>
        cursor >= task.TotalSteps ? "结束" : task.Flow.Steps[cursor].Name;

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
