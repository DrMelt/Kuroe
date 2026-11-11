using Kuroe.Flow.Shared.Display;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Pending;
using Spectre.Console;

namespace Kuroe.Console.Views;

/// <summary>宿主侧呈现：状态与时间取自 InfoLabels，样式、模型名与单行摘要由宿主给出。</summary>
internal static class ViewLabels
{
    public static string Of(TaskState state) => InfoLabels.Of(state);

    public static string Of(NodeState state) => InfoLabels.Of(state);

    /// <summary>等待种类的展示名。</summary>
    public static string Of(PendingKind kind) => kind == PendingKind.Approve ? "待批准" : "待输入";

    /// <summary>等待提问一行能看到的文本长度。</summary>
    public const int QuestionLimit = 40;

    /// <summary>阻塞原因一行能看到的文本长度。</summary>
    public const int BlockLimit = 80;

    /// <summary>等待节点的提示：种类与提问，供节点行与选择项共用。</summary>
    public static string WaitingLabel(PendingNode waiting) =>
        waiting.Question is { Length: > 0 } question
            ? $"{Of(waiting.Kind)}：{OneLine(question, QuestionLimit)}"
            : Of(waiting.Kind);

    /// <summary>执行的状态，未收口时带上过程摘要。</summary>
    public static string State(ExecutionSnapshot execution) => InfoLabels.State(execution);

    public static Style StyleOf(TaskState state) => state switch
    {
        TaskState.Done => Styles.Success,
        TaskState.Running => Styles.Key,
        TaskState.Blocked or TaskState.Paused => Styles.Warning,
        _ => Styles.Hint,
    };

    public static Style StyleOf(NodeState state) => state switch
    {
        NodeState.Done => Styles.Success,
        NodeState.Running => Styles.Key,
        NodeState.Blocked => Styles.Warning,
        _ => Styles.Hint,
    };

    public static Style StyleOf(ExecutionState state) => state switch
    {
        ExecutionState.Succeeded => Styles.Success,
        ExecutionState.Failed => Styles.Error,
        ExecutionState.Running or ExecutionState.Queued => Styles.Key,
        _ => Styles.Hint,
    };

    /// <summary>过程记录内容的样式。</summary>
    public static Style StyleOf(JournalLevel level) => level switch
    {
        JournalLevel.Error => Styles.Error,
        JournalLevel.Warning => Styles.Warning,
        _ => Style.Plain,
    };

    /// <summary>执行在一行里的字段与顺序，树行、菜单项与执行清单共用。</summary>
    public static (string Id, string Round, string State, string Elapsed, string? Failures) ExecutionParts(
        ExecutionSnapshot execution) => (
        execution.Id.ToString(),
        $"第 {execution.Input.ExecutionCount} 轮",
        State(execution),
        Elapsed(execution.Elapsed),
        execution.Failures.Count > 0 ? $"失败 {execution.Failures.Count} 条" : null);

    /// <summary>执行概况一行，供菜单与清单使用。</summary>
    public static string ExecutionLabel(ExecutionSnapshot execution)
    {
        (string id, string round, string state, string elapsed, string? failures) = ExecutionParts(execution);
        List<string> parts = [id, round, state, elapsed];
        if (failures is not null)
        {
            parts.Add(failures);
        }

        return string.Join(" · ", parts);
    }

    public static string Elapsed(TimeSpan span) => InfoLabels.Elapsed(span);

    /// <summary>执行请求次数的展示名。</summary>
    public static string Requests(ExecutionUsage usage) => InfoLabels.Requests(usage);

    /// <summary>执行 token 用量的展示条目。</summary>
    public static IReadOnlyList<(string Label, string Value)> TokenEntries(ExecutionUsage usage) =>
        InfoLabels.TokenEntries(usage);

    /// <summary>最近一次请求的上下文 token 数。</summary>
    public static string Context(ExecutionUsage usage) => InfoLabels.Context(usage);

    public static string Clock(DateTimeOffset? at) => InfoLabels.Clock(at);

    /// <summary>把多行文本压成一行，超过长度时截取前缀并给出是否截断；截断不追加记号，由调用方给出提示。</summary>
    public static (string Text, bool Truncated) Truncate(string text, int limit)
    {
        string flat = text.Replace('\n', ' ').Replace('\r', ' ').Trim();

        return flat.Length <= limit ? (flat, false) : (flat[..limit], true);
    }

    /// <summary>把多行文本压成一行，超过长度时截断并加省略号。</summary>
    public static string OneLine(string text, int limit)
    {
        (string flat, bool truncated) = Truncate(text, limit);

        return truncated ? $"{flat}…" : flat;
    }
}
