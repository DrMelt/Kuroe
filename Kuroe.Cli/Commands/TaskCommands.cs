using ErrorOr;
using Kuroe.Flow.Execution.Runs;
using Kuroe.Cli.Views;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Runs;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Shared.Waiting;
using Kuroe.Flow.Execution.Tasks;
using Kuroe.Waiting;

namespace Kuroe.Cli.Commands;

/// <summary>/task 子命令的解析与执行。无参数时进入浏览器逐级下钻。</summary>
internal sealed class TaskCommands(
    TaskRegistry registry,
    TaskService tasks,
    WaitService waits,
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
        ("/task title <任务号> <文本>", "改任务标题"),
        ("/task approve <任务号>", "批准全部待批准的审批节点"),
        ("/task approve <任务号> <@节点路径>", "批准指定审批节点，多个待批准时用 @节点路径 点名"),
        ("/task answer <任务号> [<@节点路径>] <文本>", "回答等待输入的节点，多个待输入时用 @节点路径 点名"),
        ("/task rework <任务号>", "返工被阻塞的单元"),
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
                list.Print(registry.Snapshots());
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

            case ("title", >= 4) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Rename(new TaskId(task), string.Join(' ', parts[3..])), "已改标题。");
                break;

            case ("approve", 3) when Number(parts[2], "任务号") is { } task:
                Approve(task, string.Empty);
                break;

            case ("approve", 4) when Number(parts[2], "任务号") is { } task:
                Approve(task, parts[3]);
                break;

            case ("answer", >= 4) when Number(parts[2], "任务号") is { } task:
                Answer(task, parts[3..]);
                break;

            case ("rework", 3) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.Rework(new TaskId(task), null), $"{new TaskId(task)} 已返工。");
                break;

            case ("stop", 3) when Number(parts[2], "任务号") is { } task:
                results.Report(tasks.StopTask(new TaskId(task)), $"{new TaskId(task)} 已取消。");
                break;

            case ("stop", 4) when parts[2].Equals("run", StringComparison.OrdinalIgnoreCase)
                && Number(parts[3], "run号") is { } runId:
                results.Report(tasks.StopRun(new RunId(runId)), $"{new RunId(runId)} 已取消。");
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
        list.Print([task]);
    }

    /// <summary>回答待输入的节点。首词以 @ 开头且命中等待节点路径时按点名回答，唯一待输入节点不需点名。</summary>
    private void Answer(int number, string[] inputParts)
    {
        TaskId id = new(number);
        ErrorOr<List<PendingNode>> pending = waits.Pending(id);
        if (pending.IsError)
        {
            results.Reject(pending.ErrorsOrEmptyList);
            return;
        }

        HashSet<string> waiting = [.. pending.Value
            .Where(node => node.Kind == AwaitKind.Answer)
            .Select(node => node.Node.Value)];

        string? nodePath;
        string input;
        if (inputParts.Length > 1
            && inputParts[0].Length > 1
            && inputParts[0][0] == '@'
            && waiting.Contains(inputParts[0][1..]))
        {
            nodePath = inputParts[0][1..];
            input = string.Join(' ', inputParts[1..]);
        }
        else
        {
            nodePath = null;
            input = string.Join(' ', inputParts);
        }

        results.Report(waits.Answer(id, nodePath, input), $"{id} 已收到回答，继续下一步。");
    }

    /// <summary>批准审批节点。参数以 @ 开头且命中待批准节点路径时按点名批准，未点名时批准全部待批准节点。</summary>
    private void Approve(int number, string marker)
    {
        TaskId id = new(number);
        if (marker.Length <= 1 || marker[0] != '@')
        {
            results.Report(waits.Approve(id), $"{id} 已批准，继续下一步。");
            return;
        }

        ErrorOr<List<PendingNode>> pending = waits.Pending(id);
        if (pending.IsError)
        {
            results.Reject(pending.ErrorsOrEmptyList);
            return;
        }

        string path = marker[1..];
        foreach (PendingNode node in pending.Value)
        {
            if (node.Kind == AwaitKind.Approve && node.Node.Value == path)
            {
                results.Report(waits.Approve(id, [node.Node]), $"{id} 已批准，继续下一步。");

                return;
            }
        }

        terminal.Warn($"节点「{path}」不在待批准。");
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
