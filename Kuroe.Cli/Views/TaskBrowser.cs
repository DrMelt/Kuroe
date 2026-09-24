using ErrorOr;
using Kuroe.Shared.Agent;
using Kuroe.Shared.Agent.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>任务 → 步骤内 agent → agent 详情的三级下钻。进入时独占终端，退出时冲刷排队的通知。</summary>
internal sealed class TaskBrowser(
    TaskRegistry registry,
    TaskService tasks,
    TaskListView list,
    TaskDetailView detail,
    AgentDetailView agentView,
    Terminal terminal,
    ResultPrinter results)
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
            IReadOnlyList<TaskSnapshot> tasks = registry.Snapshots();
            list.Print(tasks, registry.Active);

            Item picked = Choose("选择任务",
                [.. tasks.Select(task => new Item(TaskLabel(task), "task", task.Id)), Refresh, Exit]);
            if (picked.Action == "exit")
            {
                return;
            }

            if (picked.Task is { } id)
            {
                PickAgent(id);
            }
        }
    }

    private void PickAgent(TaskId id)
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

            List<Item> items = [.. task.Steps.SelectMany(step => step.Runs)
                .OrderBy(run => run.Id.Value)
                .Select(run => new Item("  " + TaskDetailView.AgentLabel(run), "agent", Task: id, Run: run.Id))];
            AddTaskActions(items, task);
            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("任务内", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "agent" when picked.Run is { } runId:
                    ShowAgent(runId);
                    break;

                case "approve":
                    Act(tasks.Approve(id));
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

    private void ShowAgent(RunId runId)
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
            agentView.Print(task, snapshot);

            List<Item> items = [.. snapshot.Context.Seed
                .Where(message => message.Source.FromRun is not null)
                .Select(message => new Item($"↑ {message.Source.Label}", "agent", Run: message.Source.FromRun))
                .DistinctBy(item => item.Run)];

            if (snapshot is { State: RunState.Succeeded, Result.Length: > 0 })
            {
                items.Add(new Item("⇩ 采纳该结论到任务历史", "adopt", Run: runId));
            }

            if (!snapshot.IsSettled)
            {
                items.Add(new Item("✕ 取消该 agent", "stopRun", Run: runId));
            }

            items.Add(Refresh);
            items.Add(Back);

            Item picked = Choose("agent 详情", items);
            switch (picked.Action)
            {
                case "back":
                    return;

                case "agent" when picked.Run is { } upstream:
                    runId = upstream;
                    break;

                case "adopt":
                    Act(tasks.Adopt(runId));
                    break;

                case "stopRun":
                    Act(tasks.StopRun(runId));
                    break;
            }
        }
    }

    private static void AddTaskActions(List<Item> items, TaskSnapshot task)
    {
        if (task.State == TaskState.AwaitingApproval)
        {
            items.Add(new Item("✓ 批准当前步骤，开下一步", "approve", Task: task.Id));
        }

        if (task.State == TaskState.Blocked)
        {
            items.Add(new Item("↺ 返工被阻塞的单元", "rework", Task: task.Id));
        }

        if (task.State is not TaskState.Done and not TaskState.Canceled)
        {
            items.Add(new Item("✕ 取消整个任务", "stopTask", Task: task.Id));
        }
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
        $"#{task.Id.Value} {task.Title} · {Labels.Of(task.State)} · 步骤 {task.FrontierSteps}/{task.TotalSteps}"
        + $" · {task.LiveRuns} 在跑";
}

