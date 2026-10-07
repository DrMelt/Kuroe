using ErrorOr;
using Kuroe.Executions.Runs;
using Kuroe.Cli.Views;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli.Commands;

/// <summary>/task 子命令的解析与执行。无参数时进入浏览器逐级下钻。</summary>
internal sealed class TaskCommands(
    TaskRegistry registry,
    TaskService tasks,
    TaskListView list,
    TaskDetailView detail,
    RunDetailView runView,
    TaskBrowser browser,
    Terminal terminal,
    ResultView results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/task", "打开任务浏览器，逐级进入 run 详情"),
        ("/task list", "列出任务"),
        ("/task new <目标>", "提交任务，按默认流程启动 run"),
        ("/task new --flow <流程> <目标>", "用指定流程提交任务"),
        ("/task show <任务号>", "打印该任务的节点与 run"),
        ("/task run <run号>", "打印该 run 的上下文来源与过程"),
        ("/task use <任务号>", "把前台对话切到该任务"),
        ("/task title <任务号> <文本>", "改任务标题"),
        ("/task approve <任务号>", "批准待批准的产出"),
        ("/task approve <任务号> run <run号>", "批准单个 run 的产出"),
        ("/task answer <任务号> [<@节点名>] <文本>", "回答等待输入的节点，多个待输入时用 @节点名 点名"),
        ("/task rework <任务号>", "返工被阻塞的单元"),
        ("/task adopt <run号>", "把 run 结论写进任务历史"),
        ("/task stop <任务号>", "取消任务"),
        ("/task stop run <run号>", "取消单个 run"),
        ("/task clear", "丢掉已完成或已取消的任务"),
    ];

    private const string FlowOption = "--flow";

    public void Run(string[] parts)
    {
        const string usage = "用法：/task 浏览，/task new <目标>，/task show <任务号>，/task run <run号>，/help 看全部";
        string subcommand = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch ((subcommand, parts.Length))
        {
            case (_, 1):
                browser.Browse();
                break;

            case ("list", 2):
                list.Print(registry.Snapshots(), registry.Active);
                break;

            case ("new", >= 3):
                Submit(parts);
                break;

            case ("show", 3) when Number(parts[2], "任务号") is { } task:
                Show(task);
                break;

            case ("run", 3) when Number(parts[2], "run号") is { } run:
                ShowRun(run);
                break;

            case ("use", 3) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Focus(new TaskId(task)), "已切换。");
                break;

            case ("title", >= 4) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Rename(new TaskId(task), string.Join(' ', parts[3..])), "已改标题。");
                break;

            case ("approve", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.Approve(new TaskId(task)));
                break;

            case ("approve", 5) when parts[3].Equals("run", StringComparison.OrdinalIgnoreCase)
                && Number(parts[2], "任务号") is { } task
                && Number(parts[4], "run号") is { } run:
                Act(tasks.Approve(new TaskId(task), [new RunId(run)]));
                break;

            case ("answer", >= 4) when Number(parts[2], "任务号") is { } task:
                Answer(task, parts[3..]);
                break;

            case ("rework", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.Rework(new TaskId(task), null));
                break;

            case ("adopt", 3) when Number(parts[2], "run号") is { } runId:
                Act(tasks.Adopt(new RunId(runId)));
                break;

            case ("stop", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.StopTask(new TaskId(task)));
                break;

            case ("stop", 4) when parts[2].Equals("run", StringComparison.OrdinalIgnoreCase)
                && Number(parts[3], "run号") is { } runId:
                Act(tasks.StopRun(new RunId(runId)));
                break;

            case ("clear", 2):
                terminal.Ok($"已丢掉 {registry.ClearSettled()} 个任务。");
                break;

            default:
                terminal.Hint(usage);
                break;
        }
    }

    private void Submit(string[] parts)
    {
        FlowName? flow = null;
        int from = 2;
        if (parts[2].Equals(FlowOption, StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 5)
            {
                terminal.Hint($"用法：/task new {FlowOption} <流程> <目标>");
                return;
            }

            // 空白流程名视为未指定，回退默认流程
            flow = FlowName.Create(parts[3]) is { IsError: false } parsed ? parsed.Value : null;
            from = 4;
        }

        string goal = string.Join(' ', parts[from..]);
        ErrorOr<TaskSnapshot> submitted = tasks.Submit(goal, flow, null);
        if (submitted.IsError)
        {
            results.Reject(submitted.ErrorsOrEmptyList);
            return;
        }

        TaskSnapshot task = submitted.Value;
        terminal.Ok($"已提交 {task.Id}（流程 {task.Flow.Name}），{task.LiveRuns} 个 run 已启动。");
        list.Print([task], task.Id);
    }

    /// <summary>回答等待输入的节点。首词以 @ 开头且命中等待节点名时按点名回答，唯一待输入节点不需点名。</summary>
    private void Answer(int number, string[] inputParts)
    {
        ErrorOr<WorkTask> found = registry.Find(new TaskId(number));
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        TaskSnapshot snapshot = found.Value.Snapshot();
        HashSet<string> waiting = [.. snapshot.ExecutableStates
            .Where(state => state.State == NodeState.AwaitingInput)
            .Select(state => snapshot.Graph[state.Index].Name.Value)];

        string? nodeName;
        string input;
        if (inputParts.Length > 1
            && inputParts[0].Length > 1
            && inputParts[0][0] == '@'
            && waiting.Contains(inputParts[0][1..]))
        {
            nodeName = inputParts[0][1..];
            input = string.Join(' ', inputParts[1..]);
        }
        else
        {
            nodeName = null;
            input = string.Join(' ', inputParts);
        }

        Act(tasks.Answer(new TaskId(number), nodeName, input));
    }

    private void Show(int number)
    {
        ErrorOr<WorkTask> found = registry.Find(new TaskId(number));
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        detail.Print(found.Value.Snapshot());
    }

    private void ShowRun(int number)
    {
        ErrorOr<Run> run = registry.FindRun(new RunId(number));
        if (run.IsError)
        {
            results.Reject(run.ErrorsOrEmptyList);
            return;
        }

        RunSnapshot snapshot = run.Value.Snapshot();
        ErrorOr<WorkTask> owner = registry.Find(snapshot.Context.Task);
        if (owner.IsError)
        {
            results.Reject(owner.ErrorsOrEmptyList);
            return;
        }

        runView.Print(owner.Value.Snapshot(), snapshot);
    }

    private void Act(ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            results.Reject(result.ErrorsOrEmptyList);
        }
    }

    /// <summary>标识只写数字，任务号与 run 号各自唯一。</summary>
    private int? Number(string text, string label)
    {
        if (int.TryParse(text, out int value) && value > 0)
        {
            return value;
        }

        terminal.Warn($"{label}要写成数字，收到 {text}。");

        return null;
    }
}
