using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Pending;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>任务 → 节点内的执行 → 执行详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    TaskService tasks,
    PendingService pendings,
    TaskListView list,
    TaskDetailView detail,
    ExecutionDetailView executionView,
    Terminal terminal,
    ResultView results)
{
    /// <summary>一个菜单项：进下一级、执行动作或返回。</summary>
    private sealed record Item(string Label, string Action, TaskId? Task = null, ExecutionId? Execution = null);

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
            list.Print(snapshots);

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
            terminal.NewLine();
            detail.Print(task);

            List<Item> items = [.. task.Executables.SelectMany(node => node.Executions)
                .OrderBy(execution => execution.Id.Value)
                .Select(execution => new Item("  " + TaskDetailView.ExecutionLabel(execution), "execution", Task: id, Execution: execution.Id))];
            AddTaskActions(items, task);
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
                    Act(tasks.StopTask(id));
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

        string[] waiting = [.. pending.Value.Where(node => node.Kind == PendingKind.Answer).Select(node => node.Node.Value)];

        if (waiting.Length != 1)
        {
            terminal.Hint(waiting.Length == 0
                ? "该任务没有等待输入的节点。"
                : $"该任务有 {waiting.Length} 个等待输入的节点，请用 /task answer <任务号> <@节点路径> 回答。");
            return;
        }

        TextPrompt<string> prompt = new($"回答「{waiting[0]}」：");

        Act(pendings.Answer(id, waiting[0], terminal.Prompt(prompt)));
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
            terminal.NewLine();
            executionView.Print(task, snapshot);

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

    private Item Choose(string title, IReadOnlyList<Item> items)
    {
        SelectionPrompt<Item> prompt = new()
        {
            Title = title,
            PageSize = 12,
            Converter = item => item.Label,
        };
        prompt.AddChoices(items);

        return terminal.Prompt(prompt.AddCancelResult(Back));
    }

    private static string TaskLabel(TaskSnapshot task) =>
        $"#{task.Id.Value} {task.Title} · {ViewLabels.Of(task.State)} · 节点 {task.FrontierNodes}/{task.TotalExecutableNodes}"
        + $" · {task.LiveExecutions} 在跑";
}

