using Kuroe.Execution.Shared;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Shared;
using System.Text;
using ErrorOr;
using Kuroe.Presentation.Shared;
using ExecutionSnapshot = Kuroe.Execution.Shared.ExecutionSnapshot;

namespace Kuroe.Tool.KuroeInfo;

/// <summary>任务与执行的状态查询工具。只读，不产生状态改动。</summary>
public sealed class TaskKuroeInfoTool : ITool
{
    private readonly ITaskReader _registry;

    /// <summary>详情里最近呈现的记录数。</summary>
    private const int JournalLimit = 10;

    /// <summary>输入取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>列出全部任务的函数定义。</summary>
    private static readonly ToolDefinition ListTasks = new(
        new ToolPath("KuroeInfo/ListTasks"),
        "列出全部用户任务：编号、标题、状态、节点进度与执行数。",
        []);

    /// <summary>查看任务详情的函数定义。</summary>
    private static readonly ToolDefinition GetTask = new(
        new ToolPath("KuroeInfo/GetTask"),
        "查看指定用户任务的详情：目标、流程、执行节点状态与执行列表。",
        [new ToolParameter(new ToolName("taskId"), "要查看的任务键，可用完整键或末尾 8 位", Required: true)]);

    /// <summary>查看执行详情的函数定义。</summary>
    private static readonly ToolDefinition GetExecution = new(
        new ToolPath("KuroeInfo/GetExecution"),
        "查看指定用户任务的执行详情：请求用量、输入取值与执行过程。",
        [new ToolParameter(new ToolName("executionId"), "要查看的执行号", Required: true)]);

    /// <summary>用任务与执行的索引建立载体。</summary>
    public TaskKuroeInfoTool(ITaskReader registry)
    {
        _registry = registry;
        Functions =
        [
            ListTasks.Bind(_ => List()),
            GetTask.Bind(arguments => ShowTask(arguments)),
            GetExecution.Bind(arguments => ShowExecution(arguments)),
        ];
    }

    /// <summary>任务列表。</summary>
    private ErrorOr<string> List()
    {
        IReadOnlyList<TaskSnapshot> tasks = _registry.Snapshots();
        if (tasks.Count == 0)
        {
            return "还没有任务，先提交一个任务。";
        }

        var text = new StringBuilder();
        foreach (TaskSnapshot task in tasks)
        {
            text.AppendLine($"#{task.Id.Short} · {task.Title} · {StateLabel(task)}"
                + $" · 步骤 {task.ProgressSteps}/{task.TotalSteps}"
                + $" · {task.LiveExecutions} 个执行在跑 / {task.Executions.Count} 已派"
                + $" · 最近 {InfoLabels.Clock(task.LastActivityAt)}");
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>按任务号查看详情，任务号非法或不存在时返回拒绝错误。</summary>
    private ErrorOr<string> ShowTask(ToolArguments arguments)
    {
        string? text = arguments.Text(new ToolName("taskId"));
        if (string.IsNullOrWhiteSpace(text))
        {
            return ToolErrors.Argument("任务号不能为空。");
        }

        ErrorOr<TaskSnapshot> found = _registry.FindByReference(text.Trim());
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        return DescribeTask(found.Value);
    }

    /// <summary>按执行号查看详情，执行号非法或不存在时返回拒绝错误。</summary>
    private ErrorOr<string> ShowExecution(ToolArguments arguments)
    {
        string? text = arguments.Text(new ToolName("executionId"));
        if (text is null || !int.TryParse(text, out int id) || id <= 0)
        {
            return ToolErrors.Argument($"执行号要写成数字，收到 {text ?? "空"}。");
        }

        ErrorOr<(TaskSnapshot Task, ExecutionSnapshot Execution)> found =
            _registry.FindOwnedExecution(new ExecutionId(id));
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        return DescribeExecution(found.Value.Task, found.Value.Execution);
    }
    /// <summary>任务状态展示名，执行中带上当前执行的进展。</summary>
    private static string StateLabel(TaskSnapshot task)
    {
        string? progress = task.Executions
            .FirstOrDefault(execution => execution.State.IsLive() && execution.Progress.Length > 0)?.Progress;

        return progress is { Length: > 0 }
            ? $"{InfoLabels.Of(task.State)}（{progress}）"
            : InfoLabels.Of(task.State);
    }

    /// <summary>任务详情：目标、流程、步骤状态与执行列表。</summary>
    private static string DescribeTask(TaskSnapshot task)
    {
        var text = new StringBuilder();
        text.AppendLine($"#{task.Id.Short} · {task.Title} · {StateLabel(task)}");
        text.AppendLine($"流程 {task.View.Name} · {task.View.Kind} · 步骤 {task.TotalSteps}");
        foreach (StartInput input in task.Inputs.Values)
        {
            text.AppendLine($"输入 {input.Port}：{input.Text}");
        }

        if (task.Pending.Count > 0)
        {
            text.AppendLine("等待：");
            foreach (PendingStepView pending in task.Pending)
            {
                text.AppendLine($"  {pending.Path} · {pending.Kind} · {pending.Question ?? "无提问"}");
            }
        }

        text.AppendLine("步骤：");
        foreach (FlowStepView step in task.View.Steps)
        {
            text.AppendLine($"  {step.Path} · {step.Label} · {step.State}"
                + $" · 执行 {step.Executions.Count}/{step.MaxExecutions}");
        }

        foreach (FlowGroupView group in task.View.Groups)
        {
            text.AppendLine($"分组 {group.Path} · {group.Label} · {group.State}");
        }

        text.AppendLine("执行：");
        if (task.Executions.Count == 0)
        {
            text.AppendLine("  还没有执行。");
        }
        else
        {
            foreach (ExecutionSnapshot execution in task.Executions)
            {
                text.AppendLine($"  {ExecutionLine(execution)}");
            }
        }

        AppendJournal(text, task.Journal, task.DroppedJournal);

        return text.ToString().TrimEnd();
    }

    /// <summary>执行详情。</summary>
    private static string DescribeExecution(TaskSnapshot task, ExecutionSnapshot execution)
    {
        var text = new StringBuilder();
        TimeSpan elapsed = (execution.FinishedAt ?? DateTimeOffset.UtcNow) - (execution.StartedAt ?? execution.CreatedAt);
        text.AppendLine($"{execution.Id} · {execution.Request.Kind} · 第 {execution.Request.Attempt} 轮");
        text.AppendLine($"{task.Id} {task.Title} · 目标 {execution.Request.Target}");
        string stateLabel = execution.State.IsLive() && execution.Progress.Length > 0
            ? $"{execution.State.Label()}（{execution.Progress}）"
            : execution.State.Label();
        text.AppendLine($"状态 {stateLabel}"
            + $" · {InfoLabels.Clock(execution.StartedAt)} → {InfoLabels.Clock(execution.FinishedAt)}"
            + $" · 耗时 {InfoLabels.Elapsed(elapsed)}");
        text.AppendLine($"恢复数据 {(execution.RestoreData is null ? "无" : "有")}"
            + $" · 继续自 {execution.ContinuedFrom?.ToString() ?? "—"}");
        text.AppendLine($"请求 {InfoLabels.Requests(execution.Usage)}"
            + $" · 上下文 {InfoLabels.Context(execution.Usage)}");
        foreach ((string label, string value) in InfoLabels.TokenEntries(execution.Usage))
        {
            text.AppendLine($"  {label} {value}");
        }

        foreach (string failure in execution.Failures)
        {
            text.AppendLine($"失败：{failure}");
        }

        text.AppendLine($"输入取值（{execution.Request.Inputs.Count} 条）：");
        if (execution.Request.Inputs.Count == 0)
        {
            text.AppendLine("  没有取到任何来源内容。");
        }

        foreach (ExecutionInputValue value in execution.Request.Inputs)
        {
            text.AppendLine($"  {value.Port} · {value.Source ?? "—"}");
            text.AppendLine($"    {OneLine(value.Text)}");
        }

        text.AppendLine("过程：");
        if (execution.Journal.Count == 0 && !execution.State.IsLive())
        {
            text.AppendLine("  过程记录只在运行中可见。");
        }
        else
        {
            AppendJournal(text, execution.Journal, 0);
        }

        return text.ToString().TrimEnd();
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
            string content = JournalText.Normalize(entry.Text).Replace("\n", "\n         ", StringComparison.Ordinal);
            text.AppendLine($"  [{at}] {entry.Label} > {content}");
        }
    }

    /// <summary>列表里一行的执行概况。</summary>
    private static string ExecutionLine(ExecutionSnapshot execution)
    {
        TimeSpan elapsed = (execution.FinishedAt ?? DateTimeOffset.UtcNow) - (execution.StartedAt ?? execution.CreatedAt);
        List<string> parts =
        [
            execution.Id.ToString(),
            $"第 {execution.Request.Attempt} 轮",
            $"请求 {InfoLabels.Requests(execution.Usage)}",
            execution.State.ToString(),
            InfoLabels.Elapsed(elapsed),
        ];

        return string.Join(" · ", parts);
    }

    /// <summary>上下文内容压成一行并截断。</summary>
    private static string OneLine(string text)
    {
        string flat = text.Replace('\n', ' ').Replace('\r', ' ');

        return flat.Length <= ValueLimit ? flat : string.Concat(flat.AsSpan(0, ValueLimit), "…");
    }
}