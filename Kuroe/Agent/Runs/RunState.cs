namespace Kuroe.Agent.Runs;

/// <summary>agent 的执行状态，只表达跑没跑完。流程结论见 <see cref="Workflows.UnitVerdict"/>。</summary>
public enum RunState
{
    /// <summary>已登记，等待并发额度。</summary>
    Queued,

    /// <summary>正在执行。</summary>
    Running,

    /// <summary>请求正常结束。</summary>
    Succeeded,

    /// <summary>请求失败或步骤未收口。</summary>
    Failed,

    /// <summary>被取消。</summary>
    Canceled,
}

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
