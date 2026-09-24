namespace Kuroe.Shared.Agent.Runs;

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
