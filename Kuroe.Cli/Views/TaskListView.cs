using Kuroe.Agent;
using Kuroe.Workflows.Tasks;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>任务列表：一行一个任务，标出当前对话所在的任务。</summary>
internal sealed class TaskListView(Terminal terminal)
{
    private readonly Terminal _terminal = terminal;

    public void Print(IReadOnlyList<TaskSnapshot> tasks, TaskId? active)
    {
        if (tasks.Count == 0)
        {
            _terminal.Hint("还没有任务，用 /task new <目标> 提交一个。");
            return;
        }

        Grid grid = Terminal.Columns(7);
        grid.AddRow(Header("#"), Header("标题"), Header("流程"), Header("步骤"), Header("状态"), Header("agent"), Header("最近"));
        foreach (TaskSnapshot task in tasks)
        {
            int total = task.Steps.Sum(step => step.Runs.Count);
            grid.AddRow(
                new Text(active == task.Id ? $">#{task.Id.Value}" : $"#{task.Id.Value}", Styles.Key),
                new Text(task.Title),
                new Text(task.Flow.Name),
                new Text($"{task.FrontierSteps}/{task.TotalSteps}"),
                new Text(Labels.Of(task.State), Labels.StyleOf(task.State)),
                new Text($"{task.LiveRuns} 在跑 / {total} 已派"),
                new Text(Labels.Clock(task.LastActivityAt), Styles.Hint));
        }

        _terminal.NewLine();
        _terminal.Write(grid);
    }

    private static Text Header(string label) => new(label, Styles.Hint);
}
