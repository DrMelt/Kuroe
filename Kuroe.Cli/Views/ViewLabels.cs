using Kuroe.Executions.Runs;
using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>宿主侧的状态与耗时呈现。库内同样要用的展示名（角色、run 状态）由库侧的 Label 扩展给出，
/// 只在宿主出现的（任务与单元状态、展开方式）在这里。</summary>
internal static class ViewLabels
{
    public static string Of(TaskState state) => state switch
    {
        TaskState.Running => "执行中",
        TaskState.AwaitingApproval => "待批准",
        TaskState.Blocked => "已阻塞",
        TaskState.Done => "已完成",
        TaskState.Canceled => "已取消",
        _ => state.ToString(),
    };

    public static string Of(NodeState state) => state switch
    {
        NodeState.Pending => "待执行",
        NodeState.Running => "推进中",
        NodeState.AwaitingApproval => "待批准",
        NodeState.Blocked => "已阻塞",
        NodeState.Done => "已完成",
        NodeState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>执行节点的展开方式。</summary>
    public static string Of(NodeMode mode) => mode == NodeMode.PerItem ? "按条目" : "整节点";

    /// <summary>执行节点产出后是否等人放行。</summary>
    public static string Of(NodeGate gate) => gate == NodeGate.Review ? "需批准" : "自动放行";

    /// <summary>run 的状态，有工具调用在进行时带上它。</summary>
    public static string State(RunSnapshot run) =>
        run.IsSettled || run.Progress.Length == 0
            ? run.State.Label()
            : $"{run.State.Label()}（{run.Progress}）";

    public static Style StyleOf(TaskState state) => state switch
    {
        TaskState.Done => Styles.Success,
        TaskState.Running => Styles.Key,
        TaskState.AwaitingApproval or TaskState.Blocked => Styles.Warning,
        _ => Styles.Hint,
    };

    public static string Item(int? itemIndex) => itemIndex is { } index ? $"条目 {index + 1}" : "整体";

    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    public static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm:ss");

    public static string Clock(DateTimeOffset? at) => at is null ? "—" : Clock(at.Value);
}
