using Kuroe.Flow.Shared.RuntimeGraph.Execution;

namespace Kuroe.Flow.Runtime;

/// <summary>状态的展示名与终态判定。</summary>
public static class ExecutionStates
{
    /// <summary>状态的展示名。</summary>
    public static string Label(this ExecutionState state) => state switch
    {
        ExecutionState.Queued => "排队中",
        ExecutionState.Running => "执行中",
        ExecutionState.Succeeded => "已完成",
        ExecutionState.Failed => "失败",
        ExecutionState.Canceled => "已取消",
        ExecutionState.Interrupted => "已中断",
        _ => state.ToString(),
    };

    /// <summary>不再变化。</summary>
    public static bool IsSettled(this ExecutionState state) =>
        state is ExecutionState.Succeeded or ExecutionState.Failed or ExecutionState.Canceled or ExecutionState.Interrupted;

    /// <summary>还在排队或正在执行。</summary>
    public static bool IsLive(this ExecutionState state) => !state.IsSettled();
}
