namespace Kuroe.Flow.Node.RuntimeGraph;

/// <summary>一个执行节点的执行状态。</summary>
public enum NodeState
{
    /// <summary>尚未激活或输入未齐备。</summary>
    Pending,

    /// <summary>有执行在跑。</summary>
    Running,

    /// <summary>停在执行错误、未收口或可恢复的中断，等待恢复、重新执行或放行。</summary>
    Blocked,

    /// <summary>产出已发布，后续不会再自动执行。</summary>
    Done,

    /// <summary>随任务取消而终止。</summary>
    Canceled,
}
