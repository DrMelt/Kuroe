namespace Kuroe.Flow.Shared.Execution;

/// <summary>一次执行的状态，只表达跑没跑完。</summary>
public enum ExecutionState
{
    /// <summary>入队后等待并发额度。</summary>
    Queued,

    /// <summary>正在执行。</summary>
    Running,

    /// <summary>请求正常结束。</summary>
    Succeeded,

    /// <summary>请求失败或执行节点未收口。</summary>
    Failed,

    /// <summary>被取消。</summary>
    Canceled,
}
