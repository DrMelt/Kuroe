using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Pending;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>任务列表 → 任务概览与执行 → 执行详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    TaskService tasks,
    PendingService pendings,
    TaskListView list,
    TaskOverviewView overview,
    TaskTreeView tree,
    ExecutionDetailView executionView,
    Terminal terminal,
    ResultView results)
{
    /// <summary>一个菜单项：进下一级、执行动作或返回。</summary>
    private sealed record Item(
        string Label,
        string Action,
        TaskId? Task = null,
        ExecutionId? Execution = null,
        string? Node = null);

    private static readonly Item Back = new("← 返回上一级", "back");
    private static readonly Item Exit = new("✕ 退出浏览", "exit");
    private static readonly Item Refresh = new("⟳ 刷新", "refresh");

    public void Browse()
    {
        using (terminal.Exclusive())
        {
            PickTask();
        }
    }

    private void PickTask()
    {
        while (true)
        {
            IReadOnlyList<TaskSnapshot> snapshots = registry.Snapshots();
            terminal.Page("任务列表");
            list.Print(snapshots);
            terminal.NewLine();

            Item picked = Choose("选择任务",
                [.. snapshots.Select(task => new Item(TaskLabel(task), "task", task.Id)), Refresh, Exit]);
            if (picked.Action == "exit")
            {
                return;
            }

            if (picked.Task is { } id)
            {
                PickExecution(id);
            }
        }
    }

    private void PickExecution(TaskId id)
    {
        while (true)
        {
            if (registry.Find(id) is not { IsError: false } found)
            {
                return;
            }

            TaskSnapshot task = found.Value.Snapshot();

            terminal.Page($"任务列表 › {task.Id} {task.Title}");
            overview.Print(task);
            terminal.NewLine();

            List<Item> items = [.. task.Executables.SelectMany(node => node.Executions)
                .OrderBy(execution => execution.Id.Value)
                .Select(execution => new Item(ExecutionItemLabel(execution), "execution", Task: id, Execution: execution.Id))];
            AddTaskActions(items, task);
            items.Add(new Item("≡ 查看节点树", "tree", Task: id));
            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("任务内", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "execution" when picked.Execution is { } executionId:
                    ShowExecution(executionId);
                    break;

                case "tree":
                    tree.Print(task);
                    terminal.NewLine();
                    break;

                case "approve":
                    Act(pendings.Approve(id));
                    break;

                case "answer":
                    PromptAnswer(id);
                    break;

                case "rework":
                    Act(tasks.Rework(id));
                    break;

                case "stopTask":
                    ConfirmStopTask(id);
                    break;

                case "stopExecution" when picked.Execution is { } target:
                    Act(tasks.StopExecution(target));
                    break;
            }
        }
    }

    private void PromptAnswer(TaskId id)
    {
        ErrorOr<List<PendingNode>> pending = pendings.Pending(id);
        if (pending.IsError)
        {
            return;
        }

        List<PendingNode> waiting = [.. pending.Value.Where(node => node.Kind == PendingKind.Answer)];
        if (waiting.Count == 0)
        {
            terminal.Hint("该任务没有等待输入的节点。");
            return;
        }

        if (registry.Find(id) is not { IsError: false } found)
        {
            return;
        }

        TaskSnapshot task = found.Value.Snapshot();
        List<Item> items = [.. waiting.Select(node => new Item(
            AnswerItemLabel(task, node), "answer", Node: node.Node.Value))];

        if (Choose("选择要回答的节点", items).Node is not { } nodePath)
        {
            return;
        }

        TextPrompt<string> prompt = new($"回答「{nodePath}」：");

        Act(pendings.Answer(id, nodePath, terminal.Prompt(prompt)));
    }

    private static string AnswerItemLabel(TaskSnapshot task, PendingNode node)
    {
        ExecutableNode? executable = task.Graph[node.Node] as ExecutableNode;

        return $"{node.Node.Value} · {ViewLabels.WaitingLabel(node, executable?.Type)}";
    }

    private void ShowExecution(ExecutionId executionId)
    {
        while (true)
        {
            if (registry.FindExecution(executionId) is not { IsError: false } foundExecution)
            {
                return;
            }

            ExecutionSnapshot snapshot = foundExecution.Value.Snapshot();
            if (registry.Find(snapshot.Input.Task) is not { IsError: false } foundTask)
            {
                return;
            }

            TaskSnapshot task = foundTask.Value.Snapshot();

            terminal.Page($"任务列表 › {task.Id} {task.Title} › {snapshot.Id} {snapshot.Input.Node}");
            executionView.Print(task, snapshot);
            terminal.NewLine();

            List<Item> items = [.. snapshot.Input.Inputs
                .Where(value => value.Origin.FromExecution is not null)
                .Select(value => new Item($"↑ {value.Origin.Label}", "execution", Execution: value.Origin.FromExecution))
                .DistinctBy(item => item.Execution)];

            if (!snapshot.State.IsSettled())
            {
                items.Add(new Item("✕ 取消该执行", "stopExecution", Execution: executionId));
            }

            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("执行详情", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "execution" when picked.Execution is { } upstream:
                    executionId = upstream;
                    break;

                case "stopExecution":
                    Act(tasks.StopExecution(executionId));
                    break;
            }
        }
    }

    /// <summary>取消整个任务不可恢复，选择后要求再次确认，默认停在保留任务的选项。</summary>
    private void ConfirmStopTask(TaskId id)
    {
        Item keep = new("← 返回，保留任务", "keep");
        Item stop = new("✕ 确认取消整个任务", "stop");

        if (Choose($"取消{id}？", [keep, stop]).Action == "stop")
        {
            Act(tasks.StopTask(id));
        }
    }

    private void AddTaskActions(List<Item> items, TaskSnapshot task)
    {
        if (task.State is TaskState.Done or TaskState.Canceled)
        {
            return;
        }

        // 等待与阻塞可以并存，任务级状态只表达阻塞，入口按节点的实际停留各自给出
        List<PendingNode> waiting = pendings.Pending(task.Id) is { IsError: false } found ? found.Value : [];
        bool blocked = task.ExecutableStates.Any(state => state.State == NodeState.Blocked);

        if (waiting.Any(node => node.Kind == PendingKind.Approve))
        {
            items.Add(new Item("✓ 批准待批准的节点，开下一步", "approve", Task: task.Id));
        }

        if (waiting.Any(node => node.Kind == PendingKind.Answer))
        {
            items.Add(new Item("✎ 回答等待输入的节点", "answer", Task: task.Id));
        }

        if (blocked)
        {
            items.Add(new Item("↺ 返工被阻塞的节点", "rework", Task: task.Id));
        }

        items.Add(new Item("✕ 取消整个任务", "stopTask", Task: task.Id));
    }

    private void Act(ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            results.Reject(result.ErrorsOrEmptyList);
        }
    }

    private Item Choose(string title, IReadOnlyList<Item> items) =>
        terminal.Prompt(new MenuPrompt<Item>(title, items, item => item.Label, Back));

    private static string ExecutionItemLabel(ExecutionSnapshot execution) =>
        $"{execution.Input.Node} · {ViewLabels.ExecutionLabel(execution)}";

    private static string TaskLabel(TaskSnapshot task) =>
        $"#{task.Id.Value} {task.Title} · {ViewLabels.Of(task.State)}";
}

