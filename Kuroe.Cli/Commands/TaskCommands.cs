using ErrorOr;
using Kuroe.Agent;
using Kuroe.Agent.Runs;
using Kuroe.Cli.Views;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Cli.Commands;

/// <summary>/task 子命令的解析与执行。无参数时进入浏览器逐级下钻。</summary>
internal sealed class TaskCommands(
    TaskRegistry registry,
    TaskService tasks,
    TaskListView list,
    TaskDetailView detail,
    AgentDetailView agent,
    TaskBrowser browser,
    Terminal terminal,
    ResultPrinter results)
{
    /// <summary>该命令族的帮助行。</summary>
    public static IReadOnlyList<(string Command, string Description)> Help { get; } =
    [
        ("/task", "打开任务浏览器，逐级进入 agent 详情"),
        ("/task list", "列出任务"),
        ("/task new <目标>", "提交任务，按默认流程开第一步"),
        ("/task new --flow <流程> <目标>", "用指定流程提交任务"),
        ("/task show <任务号>", "打印该任务的步骤与 agent"),
        ("/task agent <agent号>", "打印该 agent 的上下文来源与过程"),
        ("/task use <任务号>", "把前台对话切到该任务"),
        ("/task title <任务号> <文本>", "改任务标题"),
        ("/task approve <任务号>", "批准等待放行的步骤"),
        ("/task rework <任务号>", "返工被阻塞的单元"),
        ("/task adopt <agent号>", "把 agent 结论写进任务历史"),
        ("/task stop <任务号>", "取消任务"),
        ("/task stop agent <agent号>", "取消单个 agent"),
        ("/task clear", "丢掉已完成或已取消的任务"),
    ];

    private const string FlowOption = "--flow";

    public void Run(string[] parts)
    {
        const string usage = "用法：/task 浏览，/task new <目标>，/task show <任务号>，/task agent <agent号>，/task help 看全部";
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

            case ("agent", 3) when Number(parts[2], "agent号") is { } run:
                ShowAgent(run);
                break;

            case ("use", 3) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Use(new TaskId(task)), "已切换。");
                break;

            case ("title", >= 4) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Rename(new TaskId(task), string.Join(' ', parts[3..])), "已改标题。");
                break;

            case ("approve", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.Approve(new TaskId(task)));
                break;

            case ("rework", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.Rework(new TaskId(task), null));
                break;

            case ("adopt", 3) when Number(parts[2], "agent号") is { } run:
                Act(tasks.Adopt(new RunId(run)));
                break;

            case ("stop", 3) when Number(parts[2], "任务号") is { } task:
                Act(tasks.StopTask(new TaskId(task)));
                break;

            case ("stop", 4) when parts[2].Equals("agent", StringComparison.OrdinalIgnoreCase)
                && Number(parts[3], "agent号") is { } run:
                Act(tasks.StopRun(new RunId(run)));
                break;

            case ("clear", 2):
                terminal.Ok($"已丢掉 {registry.ClearFinished()} 个任务。");
                break;

            default:
                terminal.Hint(usage);
                break;
        }
    }

    private void Submit(string[] parts)
    {
        string? flow = null;
        int from = 2;
        if (parts[2].Equals(FlowOption, StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 5)
            {
                terminal.Hint($"用法：/task new {FlowOption} <流程> <目标>");
                return;
            }

            flow = parts[3];
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
        terminal.Ok($"已提交 {task.Id}（流程 {task.Flow.Name}），{task.LiveRuns} 个 agent 已派出。");
        list.Print([task], task.Id);
    }

    private void Show(int number)
    {
        ErrorOr<AgentTask> found = registry.Find(new TaskId(number));
        if (found.IsError)
        {
            results.Reject(found.ErrorsOrEmptyList);
            return;
        }

        detail.Print(found.Value.Snapshot());
    }

    private void ShowAgent(int number)
    {
        ErrorOr<AgentRun> run = registry.FindRun(new RunId(number));
        if (run.IsError)
        {
            results.Reject(run.ErrorsOrEmptyList);
            return;
        }

        RunSnapshot snapshot = run.Value.Snapshot();
        ErrorOr<AgentTask> owner = registry.Find(snapshot.Context.Task);
        if (owner.IsError)
        {
            results.Reject(owner.ErrorsOrEmptyList);
            return;
        }

        agent.Print(owner.Value.Snapshot(), snapshot);
    }

    private void Act(ErrorOr<Success> result)
    {
        if (result.IsError)
        {
            results.Reject(result.ErrorsOrEmptyList);
        }
    }

    /// <summary>标识只写数字，任务号与 agent 号各自唯一。</summary>
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
