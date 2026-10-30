using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;

namespace Kuroe.Flow.Shared.Display;

/// <summary>库与宿主共用的状态与时间展示名。</summary>
public static class InfoLabels
{
    /// <summary>任务状态的展示名。</summary>
    public static string Of(TaskState state) => state.Label();

    /// <summary>执行节点状态的展示名。</summary>
    public static string Of(NodeState state) => state switch
    {
        NodeState.Pending => "待执行",
        NodeState.Running => "执行中",
        NodeState.Blocked => "已阻塞",
        NodeState.Done => "已完成",
        NodeState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>执行状态的展示名，未收口时附上过程摘要。</summary>
    public static string State(ExecutionSnapshot execution) =>
        execution.State.IsSettled() || execution.Progress.Length == 0
            ? execution.State.Label()
            : $"{execution.State.Label()}（{execution.Progress}）";

    /// <summary>耗时的展示名。</summary>
    public static string Elapsed(TimeSpan span) => span.TotalMinutes < 1
        ? $"{span.TotalSeconds:0}秒"
        : $"{(int)span.TotalMinutes}分{span.Seconds:00}秒";

    /// <summary>时刻的展示名，未发生时用占位。</summary>
    public static string Clock(DateTimeOffset? at) => at is null ? "—" : at.Value.ToLocalTime().ToString("HH:mm:ss");
}
