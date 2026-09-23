using Kuroe.Agent.Runs;
using Kuroe.Workflows;
using Kuroe.Workflows.Flows;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>宿主侧的状态与耗时呈现。库内同样要用的展示名（角色、agent 状态）由库侧的 Label 扩展给出，
/// 只在宿主出现的（任务与单元状态、检查处置、展开方式）在这里。</summary>
internal static class Labels
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

    public static string Of(UnitState state) => state switch
    {
        UnitState.Working => "推进中",
        UnitState.AwaitingApproval => "待批准",
        UnitState.Blocked => "已阻塞",
        UnitState.Done => "已完成",
        UnitState.Canceled => "已取消",
        _ => state.ToString(),
    };

    public static string Of(UnitVerdict verdict) => verdict switch
    {
        UnitVerdict.NotChecked => "未检查",
        UnitVerdict.Verified => "通过",
        UnitVerdict.Rejected => "不通过",
        _ => verdict.ToString(),
    };

    /// <summary>检查不通过的处置。</summary>
    public static string Of(RejectAction action) => action == RejectAction.Retry ? "退回返工" : "停止";

    /// <summary>步骤的展开方式。</summary>
    public static string Of(StepScope scope) => scope == StepScope.PerItem ? "按条目" : "整步";

    /// <summary>步骤产出后是否等人放行。</summary>
    public static string Of(StepGate gate) => gate == StepGate.Review ? "需批准" : "自动";

    /// <summary>agent 的状态，有工具调用在进行时带上它。</summary>
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

    public static string Item(int? itemIndex) => itemIndex is { } index ? $"条目 {index + 1}" : "整步";

    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    public static string Clock(DateTimeOffset at) => at.ToLocalTime().ToString("HH:mm:ss");

    public static string Clock(DateTimeOffset? at) => at is null ? "—" : Clock(at.Value);
}
