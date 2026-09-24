using Kuroe.Shared.Agent.Runs;

namespace Kuroe.Agent.Runs;

/// <summary>状态的展示名与终态判定。</summary>
public static class RunStates
{
    /// <summary>状态的展示名。</summary>
    public static string Label(this RunState state) => state switch
    {
        RunState.Queued => "排队中",
        RunState.Running => "执行中",
        RunState.Succeeded => "已完成",
        RunState.Failed => "失败",
        RunState.Canceled => "已取消",
        _ => state.ToString(),
    };

    /// <summary>不再变化。</summary>
    public static bool IsSettled(this RunState state) => state is RunState.Succeeded or RunState.Failed or RunState.Canceled;

    /// <summary>还在排队或正在执行。</summary>
    public static bool IsLive(this RunState state) => !state.IsSettled();
}