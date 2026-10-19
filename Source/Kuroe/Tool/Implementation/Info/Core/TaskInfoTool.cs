using System.Text;
using ErrorOr;
using Kuroe.Node.Llm;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.Execution.Process;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.StaticGraph;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Presentation;

namespace Kuroe.Tool.Info;

/// <summary>任务与执行的信息查询工具。只读，不产生状态改动。</summary>
public sealed class TaskInfoTool : ITool
{
    private readonly TaskRegistry _registry;

    /// <summary>详情里最近呈现的记录数。</summary>
    private const int JournalLimit = 10;

    /// <summary>输入取值一行能看到的文本长度。</summary>
    private const int ValueLimit = 70;

    /// <summary>本载体的函数声明。</summary>
    public IReadOnlyList<ToolFunction> Functions { get; }

    /// <summary>列出全部任务的函数定义。</summary>
    private static readonly ToolDefinition ListTasks = new(
        new ToolPath("info/ListTasks"),
        "列出全部任务：编号、标题、状态、节点进度与执行数。",
        []);

    /// <summary>查看任务详情的函数定义。</summary>
    private static readonly ToolDefinition GetTask = new(
        new ToolPath("info/GetTask"),
        "查看指定任务的详情：目标、流程、执行节点状态与执行列表。",
        [new ToolParameter(new ToolName("taskId"), "要查看的任务号", Required: true)]);

    /// <summary>查看执行详情的函数定义。</summary>
    private static readonly ToolDefinition GetExecution = new(
        new ToolPath("info/GetExecution"),
        "查看指定执行的详情：输入取值与执行过程。",
        [new ToolParameter(new ToolName("executionId"), "要查看的执行号", Required: true)]);

    /// <summary>用任务与执行的索引建立载体。</summary>
    public TaskInfoTool(TaskRegistry registry)
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
            return "还没有任务，用 /task new <目标> 提交一个。";
        }

        var text = new StringBuilder();
        foreach (TaskSnapshot task in tasks)
        {
            int total = task.Executables.Sum(node => node.Executions.Count);
            text.AppendLine($"#{task.Id.Value} · {task.Title} · {InfoLabels.Of(task.State)}"
                + $" · 节点 {task.FrontierNodes}/{task.TotalExecutableNodes}"
                + $" · {task.LiveExecutions} 个执行在跑 / {total} 已派"
                + $" · 最近 {InfoLabels.Clock(task.LastActivityAt)}");
        }

        return text.ToString().TrimEnd();
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

    /// <summary>按执行号查看详情，执行号非法或不存在时返回拒绝错误。</summary>
    private ErrorOr<string> ShowExecution(ToolArguments arguments)
    {
        string? text = arguments.Text(new ToolName("executionId"));
        if (text is null || !int.TryParse(text, out int id) || id <= 0)
        {
            return ToolErrors.Argument($"执行号要写成数字，收到 {text ?? "空"}。");
        }

        ErrorOr<Execution> found = _registry.FindExecution(new ExecutionId(id));
        if (found.IsError)
        {
            return found.ErrorsOrEmptyList;
        }

        ExecutionSnapshot execution = found.Value.Snapshot();
        ErrorOr<WorkTask> owner = _registry.Find(execution.Input.Task);
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        return DescribeExecution(owner.Value.Snapshot(), execution);
    }

    /// <summary>任务的完整详情文本。</summary>
    private static string DescribeTask(TaskSnapshot task)
    {
        var text = new StringBuilder();
        text.AppendLine($"{task.Id} · {task.Title} · 状态：{InfoLabels.Of(task.State)}");
        text.AppendLine($"目标：{task.Goal}");
        text.AppendLine($"流程：{task.Flow.Name}"
            + $"（{string.Join(" → ", task.Graph.ExecutableNodes.Select(executable => executable.Path))}）"
            + $" · 节点进度 {task.FrontierNodes}/{task.TotalExecutableNodes}");

        if (task.ExecutableStates.Count > 0)
        {
            text.AppendLine("执行节点状态：");
            foreach (ExecutableStateSnapshot node in task.ExecutableStates)
            {
                text.AppendLine($"  {node.Node} · {InfoLabels.Of(node.State)}");
            }
        }

        text.AppendLine("execution：");
        foreach (ExecutableSnapshot node in task.Executables)
        {
            if (node.Executions.Count == 0)
            {
                continue;
            }

            text.AppendLine($"  {task.OrdinalOf(node.Executable.Path)}. {node.Executable.Path}"
                + $"（{node.Executable.Kind.Value}）");
            foreach (ExecutionSnapshot execution in node.Executions)
            {
                text.AppendLine($"    {ExecutionLine(execution)}");
            }
        }

        foreach (ContainerSnapshot container in task.Containers)
        {
            text.AppendLine($"容器 {container.Path} · {InfoLabels.Of(container.State)}");
        }

        AppendJournal(text, task.Journal, task.DroppedJournal);

        return text.ToString().TrimEnd();
    }
    /// <summary>执行的完整详情文本。</summary>
    private static string DescribeExecution(TaskSnapshot task, ExecutionSnapshot execution)
    {
        ExecutionInput input = execution.Input;
        var text = new StringBuilder();
        text.AppendLine($"{execution.Id} · {input.Node} · 第 {input.ExecutionCount} 轮");
        text.AppendLine($"{task.Id} {task.Title} · 节点 {task.OrdinalOf(input.Node)}/{task.TotalExecutableNodes}"
            + $" · 模型 {(input.Declaration as KuroeNodeDeclaration)?.Model.Value ?? "(未声明)"}");
        text.AppendLine($"状态 {InfoLabels.State(execution)}"
            + $" · {InfoLabels.Clock(execution.StartedAt)} → {InfoLabels.Clock(execution.FinishedAt)}"
            + $" · 耗时 {InfoLabels.Elapsed(execution.Elapsed)}");

        foreach (string failure in execution.Failures)
        {
            text.AppendLine($"失败：{failure}");
        }

        text.AppendLine($"输入取值（{input.Inputs.Count} 条）：");
        if (input.Inputs.Count == 0)
        {
            text.AppendLine("  没有取到任何来源内容。");
        }

        foreach (ContextValue value in input.Inputs)
        {
            text.AppendLine($"  {value.Origin.Label}");
            text.AppendLine($"    {OneLine(value.Text)}");
        }

        text.AppendLine("过程：");
        AppendJournal(text, execution.Journal, execution.DroppedEntries);

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

    /// <summary>列表里一行的执行概况。</summary>
    private static string ExecutionLine(ExecutionSnapshot execution)
    {
        List<string> parts =
        [
            execution.Id.ToString(),
            $"第 {execution.Input.ExecutionCount} 轮",
            InfoLabels.State(execution),
            InfoLabels.Elapsed(execution.Elapsed),
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
