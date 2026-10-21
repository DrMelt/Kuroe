using Kuroe.Presentation;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Pending;
using Kuroe.Workflows.Flows;

namespace Kuroe.Cli.Views;

/// <summary>任务详情：按执行节点列出已完成与在跑的执行，再列出执行节点状态与过程记录。</summary>
internal sealed class TaskDetailView(Terminal terminal, PendingService pendings)
{
    private readonly Terminal _terminal = terminal;

    public void Print(TaskSnapshot task)
    {
        _terminal.Line($"{task.Id} · {task.Title}\u3000状态：{ViewLabels.Of(task.State)}");
        _terminal.Line($"目标：{task.Goal}");
        _terminal.Line($"流程：{task.Flow.Name}（{string.Join(" → ", task.Graph.ExecutableNodes.Select(executable => executable.Path))}）"
            + $"\u3000节点进度 {task.FrontierNodes}/{task.TotalExecutableNodes}");

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
            _terminal.ToolCall($"执行节点 {task.OrdinalOf(node.Executable.Path)} · {node.Executable.Path}"
                + $"（{node.Executable.Kind.Value}）");

            if (node.Executions.Count == 0)
            {
                _terminal.Hint("  还没有执行。");
                continue;
            }

            foreach (ExecutionSnapshot execution in node.Executions)
            {
                _terminal.Line($"  {ExecutionLabel(execution)}");
            }
        }

        _terminal.NewLine();
        _terminal.Line("过程记录：");
        JournalView.Print(_terminal, task.Journal, task.DroppedJournal);
    }

    /// <summary>各执行节点的执行状态与等待与端口。</summary>
    private void PrintUnits(TaskSnapshot task)
    {
        if (task.ExecutableStates.Count == 0)
        {
            return;
        }

        List<PendingNode> waiting = pendings.Pending(task.Id) is { IsError: false } found ? found.Value : [];
        HashSet<NodePath> pending = [.. waiting.Select(node => node.Node)];

        _terminal.NewLine();
        _terminal.Line("执行节点状态：");
        foreach (ExecutableStateSnapshot node in task.ExecutableStates)
        {
            ExecutableNode? asked = task.Graph[node.Node] as ExecutableNode;
            string question = pending.Contains(node.Node) && NodeParameterView.Question(asked?.Type) is { Length: > 0 } questionText
                    ? $" · {questionText}"
                    : string.Empty;
            string ports = asked is { Outputs.Count: > 0 } ported
                ? $" · 端口：{string.Join('、', ported.Outputs.Select(port => port.Label()))}"
                : string.Empty;
            int blocks = NodeParameterView.Llm(asked?.Type)?.SystemPrompt.Count ?? 0;
            string systemPrompt = blocks > 0 ? $" · 系统指令：{blocks} 块" : string.Empty;
            _terminal.Line($"  {task.Graph[node.Node].Path} · {ViewLabels.Of(node.State)}{question}{ports}{systemPrompt}");
        }
    }

    /// <summary>列表里一行的执行概况。</summary>
    internal static string ExecutionLabel(ExecutionSnapshot execution)
    {
        List<string> parts =
        [
            execution.Id.ToString(),
            $"第 {execution.Input.ExecutionCount} 轮",
            ViewLabels.State(execution),
            ViewLabels.Elapsed(execution.Elapsed),
        ];

        return string.Join(" · ", parts);
    }
}
