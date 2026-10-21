using Kuroe.Flow.Shared.Execution;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Presentation;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>宿主侧呈现。状态、时间与执行状态的展示名均由库侧 InfoLabels 给出，StyleOf 是宿主专属。</summary>
internal static class ViewLabels
{
    public static string Of(TaskState state) => InfoLabels.Of(state);

    public static string Of(NodeState state) => InfoLabels.Of(state);

    /// <summary>执行的状态，未收口时带上过程摘要。</summary>
    public static string State(ExecutionSnapshot execution) => InfoLabels.State(execution);

    public static Style StyleOf(TaskState state) => state switch
    {
        TaskState.Done => Styles.Success,
        TaskState.Running => Styles.Key,
        TaskState.Blocked => Styles.Warning,
        _ => Styles.Hint,
    };

    public static string Elapsed(TimeSpan span) => InfoLabels.Elapsed(span);

    public static string Clock(DateTimeOffset? at) => InfoLabels.Clock(at);
}
