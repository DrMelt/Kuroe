using System.Text;
using ErrorOr;
using Kuroe.Executions;
using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Executions.Tools;
using Kuroe.Shared.Executions.Turns;
using Kuroe.Shared.Workflows.Tasks;
using Kuroe.Workflows.Tasks;

namespace Kuroe.Tools.KuroeTools;

/// <summary>任务与 run 的信息查询工具。只读，不产生状态改动。</summary>
internal sealed class TaskInfoTool : ITool
{
    private readonly TaskRegistry _registry;

    /// <summary>详情里最近呈现的记录数。</summary>
    private const int JournalLimit = 10;

    /// <summary>上下文来源一行能看到的文本长度。</summary>
    private const int SeedLimit = 70;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    public TaskInfoTool(TaskRegistry registry)
    {
        _registry = registry;
        Functions =
        [
            new ToolFunction(new ToolName("ListTasks"),
                "列出全部任务：编号、标题、状态、节点进度与 run 数，标出当前对话所在任务。",
                [], _ => List(), new ToolPath("info/ListTasks")),
            new ToolFunction(new ToolName("GetTask"),
                "查看指定任务的详情：目标、流程、执行节点状态、run 列表与拆分条目。",
                [new ToolParameter(new ToolName("taskId"), "要查看的任务号", Required: true)],
                arguments => ShowTask(arguments), new ToolPath("info/GetTask")),
            new ToolFunction(new ToolName("GetRun"),
                "查看指定 run 的详情：上下文来源、执行过程与结论。",
                [new ToolParameter(new ToolName("runId"), "要查看的 run 号", Required: true)],
                arguments => ShowRun(arguments), new ToolPath("info/GetRun")),
            new ToolFunction(new ToolName("GetActiveTask"),
                "查看当前前台对话所在任务的详情。",
                [], _ => ActiveTask(), new ToolPath("info/GetActiveTask")),
        ];
    }

    /// <summary>任务列表。</summary>
    private ErrorOr<string> List()
    {
        IReadOnlyList<TaskSnapshot> tasks = _registry.Snapshots();
        if (tasks.Count == 0)
        {
            return "还没有任务，用 /task new <目标> 提交一个。";
        }

        TaskId? active = _registry.Active;
        var text = new StringBuilder();
        foreach (TaskSnapshot task in tasks)
        {
            int total = task.Executables.Sum(node => node.Runs.Count);
            text.AppendLine($"{Marker(active, task.Id)}#{task.Id.Value} · {task.Title} · {InfoLabels.Of(task.State)}"
                + $" · 节点 {task.FrontierNodes}/{task.TotalExecutableNodes}"
                + $" · run {task.LiveRuns} 在跑 / {total} 已派"
                + $" · 最近 {InfoLabels.Clock(task.LastActivityAt)}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>当前前台对话所在任务的详情。</summary>
    private ErrorOr<string> ActiveTask()
    {
        if (_registry.Active is not { } id || _registry.Find(id) is not { IsError: false } found)
        {
            return "还没有任务，先 /task new <目标> 提交一个。";
        }

        return DescribeTask(found.Value.Snapshot());
    }

    /// <summary>按任务号查看详情，任务号非法或不存在时返回拒绝错误。</summary>
    private ErrorOr<string> ShowTask(ToolArguments arguments)
    {
        string? text = arguments.Text(new ToolName("taskId"));
        if (text is null || !int.TryParse(text, out int id) || id <= 0)
        {
            return ToolErrors.Argument($"任务号要写成数字，收到 {text ?? "空"}。");
        }

        ErrorOr<WorkTask> found = _registry.Find(new TaskId(id));
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        return DescribeTask(found.Value.Snapshot());
    }

    /// <summary>按 run 号查看详情，run 号非法或不存在时返回拒绝错误。</summary>
    private ErrorOr<string> ShowRun(ToolArguments arguments)
    {
        string? text = arguments.Text(new ToolName("runId"));
        if (text is null || !int.TryParse(text, out int id) || id <= 0)
        {
            return ToolErrors.Argument($"run号要写成数字，收到 {text ?? "空"}。");
        }

        ErrorOr<Run> found = _registry.FindRun(new RunId(id));
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        RunSnapshot run = found.Value.Snapshot();
        ErrorOr<WorkTask> owner = _registry.Find(run.Context.Task);
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        return DescribeRun(owner.Value.Snapshot(), run);
    }

    /// <summary>任务的完整详情文本。</summary>
    private static string DescribeTask(TaskSnapshot task)
    {
        var text = new StringBuilder();
        text.AppendLine($"{task.Id} · {task.Title} · 状态：{InfoLabels.Of(task.State)}");
        text.AppendLine($"目标：{task.Goal}");
        text.AppendLine($"流程：{task.Flow.Name}"
            + $"（{string.Join(" → ", task.Graph.ExecutableNodes.Select(executable => executable.Path))}）"
            + $" · 节点进度 {task.FrontierNodes}/{task.TotalExecutableNodes}"
            + $" · 前台对话 {task.DialogueTurns} 回合");

        AppendSplits(text, task);

        if (task.ExecutableStates.Count > 0)
        {
            text.AppendLine("执行节点状态：");
            foreach (ExecutableStateSnapshot node in task.ExecutableStates)
            {
                string items = node.Items.Count == 0 ? string.Empty : $" · {node.CompletedItems}/{node.Items.Count} 条";
                text.AppendLine($"  {task.Graph[node.Index].Name} · {InfoLabels.Of(node.State)}{items}");
            }
        }

        text.AppendLine("run：");
        foreach (ExecutableSnapshot node in task.Executables)
        {
            if (node.Runs.Count == 0)
            {
                continue;
            }

            text.AppendLine($"  {task.OrdinalOf(node.Index)}. {node.Executable.Path}"
                + $"（{node.Executable.Output.Label()} · {InfoLabels.Of(node.Executable.Mode)} · {InfoLabels.Of(node.Executable.Gate)}）");
            foreach (RunSnapshot run in node.Runs)
            {
                text.AppendLine($"    {RunLine(run)}");
            }
        }

        foreach (ContainerSnapshot container in task.Containers)
        {
            text.AppendLine($"容器 {container.Path} · {InfoLabels.Of(container.State)}");
        }

        AppendJournal(text, task.Dialogue, task.DroppedDialogue);

        return text.ToString().TrimEnd();
    }
    /// <summary>run 的完整详情文本。</summary>
    private static string DescribeRun(TaskSnapshot task, RunSnapshot run)
    {
        RunContext context = run.Context;
        var text = new StringBuilder();
        text.AppendLine($"{run.Id} · {context.Output.Label()} · {context.NodeName} · {InfoLabels.Item(context.ItemIndex)}"
            + $" · 第 {context.ExecutionCount} 轮");
        text.AppendLine($"{task.Id} {task.Title} · 节点 {task.OrdinalOf(context.NodeIndex)}/{task.TotalExecutableNodes}"
            + $" · 模型 {context.Model}");
        text.AppendLine($"状态 {run.State.Label()}"
            + $" · {InfoLabels.Clock(run.StartedAt)} → {InfoLabels.Clock(run.FinishedAt)}"
            + $" · 耗时 {InfoLabels.Elapsed(run.Elapsed)}");

        foreach (string failure in run.Failures)
        {
            text.AppendLine($"失败：{failure}");
        }

        text.AppendLine($"指令：{context.Instruction}");
        text.AppendLine($"上下文来源（{context.Seed.Count} 条）：");
        if (context.Seed.Count == 0)
        {
            text.AppendLine("  没有装配任何已有内容。");
        }

        foreach (ContextMessage message in context.Seed)
        {
            text.AppendLine($"  {message.Source.Label} {OneLine(message.Text)}");
        }

        text.AppendLine("过程：");
        AppendJournal(text, run.Journal, run.DroppedEntries);

        if (run.Result is { Length: > 0 } result)
        {
            text.AppendLine($"结论：{result}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>拆分的条目清单。</summary>
    private static void AppendSplits(StringBuilder text, TaskSnapshot task)
    {
        foreach ((int executableIndex, PlanOutput plan) in task.Splits.OrderBy(entry => entry.Key))
        {
            text.AppendLine($"拆分 {plan.Items.Count} 条，由 {plan.Origin} 在节点「{task.Graph[executableIndex].Name}」交回：");
            foreach (PlanItem item in plan.Items)
            {
                string branch = item.Branch is { } name ? $"（{name}）" : string.Empty;
                text.AppendLine($"  {item.Index + 1}. {item.Title}{branch}");
            }
        }
    }

    /// <summary>过程记录的最近条目。<paramref name="dropped"/> 是更早已丢弃的条数。</summary>
    private static void AppendJournal(StringBuilder text, IReadOnlyList<JournalEntry> entries, int dropped)
    {
        text.AppendLine("过程记录：");
        if (dropped > 0)
        {
            text.AppendLine($"  更早的 {dropped} 条记录已丢弃。");
        }

        List<JournalEntry> shown = [.. entries.TakeLast(JournalLimit)];
        if (shown.Count == 0)
        {
            text.AppendLine("  还没有记录。");
            return;
        }

        foreach (JournalEntry entry in shown)
        {
            string at = InfoLabels.Clock(entry.At);
            switch (entry)
            {
                case PromptEntry prompt:
                    text.AppendLine($"  [{at}] 输入 > {prompt.Text}");
                    break;

                case TextEntry textEntry:
                    text.AppendLine($"  [{at}] {textEntry.Text}");
                    break;

                case ToolCallEntry call:
                    text.AppendLine($"  [{at}] 工具 > {call.Call.Name} {call.Call.Arguments}");
                    text.AppendLine($"         结果 > {call.Call.Outcome}");
                    break;

                case ErrorEntry error:
                    text.AppendLine($"  [{at}] 错误 > {error.Text}");
                    break;

                case DiscardedEntry:
                    text.AppendLine($"  [{at}] 本轮未计入上下文。");
                    break;
            }
        }
    }

    /// <summary>列表里一行的 run 概况。</summary>
    private static string RunLine(RunSnapshot run) =>
        string.Join(" · ",
            run.Id.ToString(),
            InfoLabels.Item(run.Context.ItemIndex),
            $"第 {run.Context.ExecutionCount} 轮",
            run.State.Label(),
            InfoLabels.Elapsed(run.Elapsed));

    /// <summary>当前对话所在任务的标记。</summary>
    private static string Marker(TaskId? active, TaskId id) => active == id ? "当前对话 " : string.Empty;

    /// <summary>上下文内容压成一行并截断。</summary>
    private static string OneLine(string text)
    {
        string flat = text.Replace('\n', ' ').Replace('\r', ' ');

        return flat.Length <= SeedLimit ? flat : string.Concat(flat.AsSpan(0, SeedLimit), "…");
    }
}
