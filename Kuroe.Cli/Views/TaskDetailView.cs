using Kuroe.Executions;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Graph;
using Kuroe.Shared.Workflows.Tasks;

namespace Kuroe.Cli.Views;

/// <summary>任务详情：按执行节点列出已执行与在执行的 run，再列出条目与过程记录。</summary>
internal sealed class TaskDetailView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task)
    {
        _terminal.Line($"{task.Id} · {task.Title}\u3000状态：{ViewLabels.Of(task.State)}");
        _terminal.Line($"目标：{task.Goal}");
        _terminal.Line($"流程：{task.Flow.Name}（{string.Join(" → ", task.Graph.ExecutableNodes.Select(executable => executable.Path))}）"
            + $"\u3000节点进度 {task.FrontierNodes}/{task.TotalExecutableNodes}");

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
                _terminal.Line($"  {container.Path} · {ViewLabels.Of(container.State)}");
            }
        }

        _terminal.NewLine();
        foreach (ExecutableSnapshot node in task.Executables)
        {
            _terminal.ToolCall($"执行节点 {task.OrdinalOf(node.Index)} · {node.Executable.Path}"
                + $"（{node.Executable.Execution.Output.Label()} · {ViewLabels.Of(node.Executable.Execution.Mode)} · {ViewLabels.Of(node.Executable.Gate)}）");

            if (node.Runs.Count == 0)
            {
                _terminal.Hint("  还没有 run。");
                continue;
            }

            foreach (RunSnapshot run in node.Runs)
            {
                IReadOnlyList<RunId> awaitingRuns = task.ExecutableStates.FirstOrDefault(state => state.Index == node.Index)?.AwaitingRuns ?? [];
                _terminal.Line($"  {RunLabel(run, awaitingRuns.Contains(run.Id))}");
            }
        }

        _terminal.NewLine();
        _terminal.Line("过程记录：");
        JournalView.Print(_terminal, task.Journal, task.DroppedJournal);
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
            string question = node.State == NodeState.AwaitingInput
                && task.Graph[node.Index] is ExecutableNode input
                && input.Execution.Question is { Length: > 0 } questionText
                    ? $" · {questionText}"
                    : string.Empty;
            string ports = task.Graph[node.Index] is ExecutableNode { Outputs.Count: > 0 } ported
                ? $" · 端口：{string.Join('、', ported.Outputs.Select(port => port.Value))}"
                : string.Empty;
            string systemPrompt = task.Graph[node.Index] is ExecutableNode { SystemPrompt.Count: > 0 } systemPrompted
                ? $" · 系统指令：{systemPrompted.SystemPrompt.Count} 块"
                : string.Empty;
            string answered = node.State == NodeState.Done
                && node.InputAnswer is { Length: > 0 } answerText
                    ? $" · 回答：{answerText}"
                    : string.Empty;
            _terminal.Line($"  {task.Graph[node.Index].Name} · {ViewLabels.Of(node.State)}{items}{question}{ports}{systemPrompt}{answered}");
        }
    }

    /// <summary>列表里一行的 run 概况，待批准的 run 标出。</summary>
    internal static string RunLabel(RunSnapshot run, bool awaiting = false)
    {
        List<string> parts =
        [
            run.Id.ToString(),
            ViewLabels.Item(run.Context.ItemIndex),
            $"第 {run.Context.ExecutionCount} 轮",
            ViewLabels.State(run),
            ViewLabels.Elapsed(run.Elapsed),
        ];
        if (awaiting)
        {
            parts.Add("待批准");
        }

        return string.Join(" · ", parts);
    }
}
