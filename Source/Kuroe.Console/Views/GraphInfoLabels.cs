using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;

namespace Kuroe.Console.Views;

/// <summary>图执行侧状态与恢复信息的展示名。</summary>
internal static class GraphInfoLabels
{
    public static string Of(NodeState state) => state switch
    {
        NodeState.Pending => "待执行",
        NodeState.Running => "执行中",
        NodeState.Blocked => "已阻塞",
        NodeState.Done => "已完成",
        NodeState.Canceled => "已取消",
        _ => state.ToString(),
    };

    public static string Of(ExecutableStateSnapshot state) =>
        state.State == NodeState.Blocked && state.CanRestore
            ? "已阻塞，可恢复"
            : Of(state.State);

    public static string RestoreData(GraphExecutionSnapshot execution) =>
        execution.HasRestoreData ? "有" : "无";

    public static string ContinuedFrom(GraphExecutionSnapshot execution) =>
        execution.ContinuedFrom?.Value.ToString() ?? "—";

    public static string State(GraphExecutionSnapshot execution) =>
        execution.State.IsSettled() || execution.Progress.Length == 0
            ? execution.State.Label()
            : $"{execution.State.Label()}（{execution.Progress}）";
}