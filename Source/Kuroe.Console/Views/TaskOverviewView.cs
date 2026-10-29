using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Pending;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Kuroe.Console.Views;

/// <summary>任务概览：任务字段、待处理节点与执行清单，不展开节点层级。</summary>
internal sealed class TaskOverviewView(Terminal terminal, PendingService pendings)
{
    private readonly Terminal _terminal = terminal;
    private readonly PendingService _pendings = pendings;

    public void Print(TaskSnapshot task)
    {
        List<(string Label, IRenderable Value)> fields =
        [
            .. task.Inputs.Values.Select(input => ($"输入 {input.Port}", (IRenderable)new Text(input.Text))),
            ("流程", new Text(task.Flow.Name.Value)),
            ("状态", new Text(ViewLabels.Of(task.State), ViewLabels.StyleOf(task.State))),
            ("创建时间", new Text(ViewLabels.Clock(task.CreatedAt), Styles.Hint)),
            ("最近活动", new Text(ViewLabels.Clock(task.LastActivityAt), Styles.Hint)),
        ];

        _terminal.Write(Blocks.Section(BoxKind.View, "概览", Blocks.Fields([.. fields])));

        PrintWaiting(task);
        PrintExecutions(task);
    }

    /// <summary>等待中的节点给出路径、种类与提问，回答与批准不需要先看节点树。</summary>
    private void PrintWaiting(TaskSnapshot task)
    {
        if (_pendings.Pending(task.Id) is not { IsError: false } found || found.Value.Count == 0)
        {
            return;
        }

        Table table = Blocks.Table(("节点", false), ("等待", false), ("提问", true));
        foreach (PendingNode node in found.Value)
        {
            ExecutableNode? executable = task.Graph[node.Node] as ExecutableNode;
            string question = NodeParameterView.Question(executable?.Type) is { Length: > 0 } text
                ? ViewLabels.OneLine(text, ViewLabels.QuestionLimit)
                : "—";
            table.AddRow(
                new Text(node.Node.Value, Styles.Key),
                new Text(ViewLabels.Of(node.Kind), Styles.Warning),
                new Text(question));
        }

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(BoxKind.View, $"待处理 {found.Value.Count} 项", table));
    }

    /// <summary>按执行节点列出执行，执行号是进入执行详情的入口。</summary>
    private void PrintExecutions(TaskSnapshot task)
    {
        IReadOnlyList<ExecutableSnapshot> executed = [.. task.Executables.Where(node => node.Executions.Count > 0)];
        if (executed.Count == 0)
        {
            if (task.Executables.Count > 0)
            {
                _terminal.NewLine();
                _terminal.Hint("还没有执行。");
            }

            return;
        }

        int total = executed.Sum(node => node.Executions.Count);
        Table table = Blocks.Table(
            ("执行", false),
            ("节点", false),
            ("轮次", false),
            ("状态", false),
            ("耗时", false),
            ("失败", false));

        foreach (ExecutableSnapshot node in executed)
        {
            foreach (ExecutionSnapshot execution in node.Executions.OrderBy(execution => execution.Id.Value))
            {
                (string id, string round, string state, string elapsed, string? failures) = ViewLabels.ExecutionParts(execution);
                table.AddRow(
                    new Text(id, Styles.Key),
                    new Text(node.Executable.Path.Value),
                    new Text(round, Styles.Hint),
                    new Text(state, ViewLabels.StyleOf(execution.State)),
                    new Text(elapsed, Styles.Hint),
                    new Text(failures ?? string.Empty, Styles.Error));
            }
        }

        _terminal.NewLine();
        _terminal.Write(Blocks.Section(BoxKind.View, $"执行 {total} 次", table));
    }
}