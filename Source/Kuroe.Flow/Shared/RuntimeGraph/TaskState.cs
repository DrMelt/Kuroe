namespace Kuroe.Flow.Shared.RuntimeGraph;

/// <summary>任务的整体状态，由取消标记、各执行节点与在跑的执行汇总得出，不单独维护。</summary>
public enum TaskState
{
    /// <summary>有执行在跑或排队。</summary>
    Running,

    /// <summary>有执行节点被阻塞，任务不再自动推进。</summary>
    Blocked,

    /// <summary>全部执行节点走完流程。</summary>
    Done,

    /// <summary>已取消。</summary>
    Canceled,
}
