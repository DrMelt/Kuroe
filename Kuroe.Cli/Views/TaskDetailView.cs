using Kuroe.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务详情：按执行节点列出已执行与在执行的 run，再列出条目与前台对话。</summary>
internal sealed class TaskDetailView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task)
    {
        _terminal.Line($"{task.Id} · {task.Title}\u3000状态：{Labels.Of(task.State)}");
        _terminal.Line($"目标：{task.Goal}");
        _terminal.Line($"流程：{task.Flow.Name}（{string.Join(" → ", task.Graph.ExecutableNodes.Select(executable => executable.Path))}）"
            + $"\u3000节点进度 {task.FrontierNodes}/{task.TotalNodes}\u3000前台对话 {task.DialogueTurns} 回合");

        if (task.Splits.Count > 0)
        {
            _terminal.NewLine();
            foreach ((int executableIndex, PlanOutput plan) in task.Splits.OrderBy(entry => entry.Key))
            {
                string nodeName = task.Graph[executableIndex].Name.Value;
                _terminal.Line($"拆分由 {plan.Origin} 在节点「{nodeName}」交回：");
                foreach (PlanItem item in plan.Items)
                {
                    string branch = item.Branch is { } name ? $"（{name}）" : string.Empty;
                    _terminal.Line($"  {item.Index + 1}. {item.Title}{branch}");
                }
            }
        }

        PrintUnits(task);

        if (task.Containers.Count > 0)
        {
            _terminal.NewLine();
            _terminal.Line("容器状态：");
            foreach (ContainerSnapshot container in task.Containers)
            {
                _terminal.Line($"  {container.Path} · {Labels.Of(container.State)}");
            }
        }

        _terminal.NewLine();
        foreach (ExecutableSnapshot node in task.Executables)
        {
            _terminal.ToolCall($"执行节点 {task.OrdinalOf(node.Index)} · {node.Executable.Path}"
                + $"（{node.Executable.Output.Label()} · {Labels.Of(node.Executable.Mode)} · {Labels.Of(node.Executable.Gate)}）");

            if (node.Runs.Count == 0)
            {
                _terminal.Hint("  还没有 run。");
                continue;
            }

            foreach (RunSnapshot run in node.Runs)
            {
                _terminal.Line($"  {RunLabel(run)}");
            }
        }

        _terminal.NewLine();
        _terminal.Line("前台对话：");
        JournalPrinter.Print(_terminal, task.Dialogue, task.DroppedDialogue);
    }

    /// <summary>各执行节点的执行状态与条目结论。</summary>
    private void PrintUnits(TaskSnapshot task)
    {
        if (task.ExecutableStates.Count == 0)
        {
            return;
        }

        _terminal.NewLine();
        _terminal.Line("执行节点状态：");
        foreach (ExecutableStateSnapshot node in task.ExecutableStates)
        {
            string items = node.Items.Count == 0 ? string.Empty : $" · {node.CompletedItems}/{node.Items.Count} 条";
            _terminal.Line($"  {task.Graph[node.Index].Name} · {Labels.Of(node.State)}{items}");
        }
    }

    /// <summary>列表里一行的 run 概况。</summary>
    internal static string RunLabel(RunSnapshot run)
    {
        List<string> parts =
        [
            run.Id.ToString(),
            Labels.Item(run.Context.ItemIndex),
            $"第 {run.Context.ExecutionCount} 轮",
            Labels.State(run),
            Labels.Elapsed(run.Elapsed),
        ];

        return string.Join(" · ", parts);
    }
}
