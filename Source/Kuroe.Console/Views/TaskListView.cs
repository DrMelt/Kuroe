using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Pending;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>任务列表：一行一个任务。</summary>
internal sealed class TaskListView(Terminal terminal, PendingService pendings)
{
    /// <summary>标题列一行能看到的文本长度。</summary>
    private const int TitleLimit = 30;

    private readonly Terminal _terminal = terminal;
    private readonly PendingService _pendings = pendings;

    public void Print(IReadOnlyList<TaskSnapshot> tasks)
    {
        if (tasks.Count == 0)
        {
            _terminal.Hint("还没有任务，用 /task new --flow <流程> <目标> 提交一个。");
            return;
        }

        Table table = Blocks.Table(
            ("#", false),
            ("标题", true),
            ("流程", false),
            ("创建时间", false),
            ("状态", false),
            ("待处理", false),
            ("最近", false));
        foreach (TaskSnapshot task in tasks)
        {
            List<PendingNode> waiting = _pendings.Pending(task.Id) is { IsError: false } found ? found.Value : [];
            table.AddRow(
                new Text($"#{task.Id.Value}", Styles.Key),
                new Text(ViewLabels.OneLine(task.Title, TitleLimit)),
                new Text(task.Flow.Name.Value),
                new Text(ViewLabels.Clock(task.CreatedAt), Styles.Hint),
                new Text(ViewLabels.Of(task.State), ViewLabels.StyleOf(task.State)),
                new Text(waiting.Count.ToString(), waiting.Count == 0 ? Styles.Hint : Styles.Warning),
                new Text(ViewLabels.Clock(task.LastActivityAt), Styles.Hint));
        }

        _terminal.Write(Blocks.Section(BoxKind.View, $"任务 {tasks.Count} 个", table));
    }
}
