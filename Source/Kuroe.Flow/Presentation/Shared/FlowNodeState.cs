namespace Kuroe.Flow.Presentation;

/// <summary>步骤或分组在某一刻的状态。</summary>
public enum FlowNodeState
{
    /// <summary>尚未开始。</summary>
    Pending,

    /// <summary>推进中。</summary>
    Running,

    /// <summary>停在阻塞。</summary>
    Blocked,

    /// <summary>已完成。</summary>
    Done,

    /// <summary>随任务取消。</summary>
    Canceled,
}