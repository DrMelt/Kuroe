using ErrorOr;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>任务 → 执行节点内 run → run 详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    TaskService tasks,
    TaskListView list,
    TaskDetailView detail,
    RunDetailView runView,
    Terminal terminal,
    ResultView results)
{
    /// <summary>一个菜单项：进下一级、执行动作或返回。</summary>
    private sealed record Item(string Label, string Action, TaskId? Task = null, RunId? Run = null);

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
                PickRun(id);
            }
        }
    }

    private void PickRun(TaskId id)
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

            HashSet<RunId> awaiting = [.. task.ExecutableStates.SelectMany(state => state.AwaitingRuns)];
            List<Item> items = [.. task.Executables.SelectMany(node => node.Runs)
                .OrderBy(run => run.Id.Value)
                .Select(run => new Item("  " + TaskDetailView.RunLabel(run, awaiting.Contains(run.Id)), "run", Task: id, Run: run.Id))];
            AddTaskActions(items, task);
            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("任务内", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "run" when picked.Run is { } runId:
                    ShowRun(runId);
                    break;

                case "approve":
                    Act(tasks.Approve(id));
                    break;

                case "answer":
                    PromptAnswer(id);
                    break;

                case "rework":
                    Act(tasks.Rework(id, null));
                    break;

                case "stopTask":
                    Act(tasks.StopTask(id));
                    break;

                case "stopRun" when picked.Run is { } target:
                    Act(tasks.StopRun(target));
                    break;
            }
        }
    }

    private void PromptAnswer(TaskId id)
    {
        if (registry.Find(id) is not { IsError: false } found)
        {
            return;
        }

        TaskSnapshot task = found.Value.Snapshot();
        string[] waiting = [.. task.ExecutableStates
            .Where(state => state.State == NodeState.AwaitingInput)
            .Select(state => task.Graph[state.Index].Name.Value)];

        if (waiting.Length != 1)
        {
            terminal.Hint(waiting.Length == 0
                ? "该任务没有等待输入的节点。"
                : $"该任务有 {waiting.Length} 个等待输入的节点，请用 /task answer <任务号> <节点名> 回答。");
            return;
        }

        TextPrompt<string> prompt = new($"回答「{waiting[0]}」：");
        string input = terminal.Prompt(prompt);
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        Act(tasks.Answer(id, waiting[0], input));
    }

    private void ShowRun(RunId runId)
    {
        while (true)
        {
            if (registry.FindRun(runId) is not { IsError: false } foundRun)
            {
                return;
            }

            RunSnapshot snapshot = foundRun.Value.Snapshot();
            if (registry.Find(snapshot.Context.Task) is not { IsError: false } foundTask)
            {
                return;
            }

            TaskSnapshot task = foundTask.Value.Snapshot();
            terminal.NewLine();
            runView.Print(task, snapshot);

            List<Item> items = [.. snapshot.Context.Seed
                .Where(message => message.Source.FromRun is not null)
                .Select(message => new Item($"↑ {message.Source.Label}", "run", Run: message.Source.FromRun))
                .DistinctBy(item => item.Run)];

            bool awaitingRun = task.ExecutableStates.Any(state =>
                state.Index == snapshot.Context.NodeIndex && state.AwaitingRuns.Contains(runId));

            if (awaitingRun)
            {
                items.Add(new Item("✓ 批准该 run 的产出", "approveRun", Task: task.Id, Run: runId));
            }

            if (!snapshot.IsSettled)
            {
                items.Add(new Item("✕ 取消该 run", "stopRun", Run: runId));
            }

            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("run 详情", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "run" when picked.Run is { } upstream:
                    runId = upstream;
                    break;

                case "approveRun" when picked.Task is { } owner:
                    Act(tasks.Approve(owner, [runId]));
                    break;

                case "stopRun":
                    Act(tasks.StopRun(runId));
                    break;
            }
        }
    }

    private static void AddTaskActions(List<Item> items, TaskSnapshot task)
    {
        if (task.State is TaskState.Done or TaskState.Canceled)
        {
            return;
        }

        // 多个等待并存时任务级状态只表达其一，入口按节点与容器的实际停留各自给出
        bool awaitingApproval = task.ExecutableStates.Any(state => state.State == NodeState.AwaitingApproval)
            || task.Containers.Any(container => container.State == NodeState.AwaitingApproval);
        bool awaitingInput = task.ExecutableStates.Any(state => state.State == NodeState.AwaitingInput);
        bool blocked = task.ExecutableStates.Any(state => state.State == NodeState.Blocked);

        if (awaitingApproval)
        {
            items.Add(new Item("✓ 批准当前节点，开下一步", "approve", Task: task.Id));
        }

        if (awaitingInput)
        {
            items.Add(new Item("✎ 回答等待输入的节点", "answer", Task: task.Id));
        }

        if (blocked)
        {
            items.Add(new Item("↺ 返工被阻塞的单元", "rework", Task: task.Id));
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
        + $" · {task.LiveRuns} 在跑";
}

