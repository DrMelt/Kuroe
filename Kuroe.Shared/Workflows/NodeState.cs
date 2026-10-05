namespace Kuroe.Shared.Workflows;

/// <summary>一个执行节点的执行状态。</summary>
public enum NodeState
{
    /// <summary>尚未激活或输入未齐备。</summary>
    Pending,

    /// <summary>有 run 在跑。</summary>
    Running,

    /// <summary>本节点产出已就绪，待批准后才向下游发布。</summary>
    AwaitingApproval,

    /// <summary>停在执行错误或未收口，等待返工或放行。</summary>
    Blocked,

    /// <summary>产出已发布，后续不会再自动执行。</summary>
    Done,

    /// <summary>随任务取消而终止。</summary>
    Canceled,
}
