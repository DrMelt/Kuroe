using Kuroe.Shared.Executions.Runs;
using Kuroe.Shared.Workflows;
using Kuroe.Shared.Workflows.Flows;
using Kuroe.Tools.KuroeTools;
using Spectre.Console;

namespace Kuroe.Cli.Views;

/// <summary>宿主侧呈现。状态、展开方式、放行方式、时间与 run 状态的展示名均由库侧 InfoLabels 给出，StyleOf 是宿主专属。</summary>
internal static class ViewLabels
{
    public static string Of(TaskState state) => InfoLabels.Of(state);

    public static string Of(NodeState state) => InfoLabels.Of(state);

    public static string Of(NodeMode mode) => InfoLabels.Of(mode);

    public static string Of(NodeGate gate) => InfoLabels.Of(gate);

    /// <summary>run 的状态，有工具调用在进行时带上它。</summary>
    public static string State(RunSnapshot run) => InfoLabels.State(run);

    public static Style StyleOf(TaskState state) => state switch
    {
        TaskState.Done => Styles.Success,
        TaskState.Running => Styles.Key,
        TaskState.AwaitingApproval or TaskState.AwaitingInput or TaskState.Blocked => Styles.Warning,
        _ => Styles.Hint,
    };

    public static string Item(int? itemIndex) => InfoLabels.Item(itemIndex);

    public static string Elapsed(TimeSpan span) => InfoLabels.Elapsed(span);

    public static string Clock(DateTimeOffset? at) => InfoLabels.Clock(at);
}
