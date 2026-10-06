namespace Kuroe.Shared.Workflows;

/// <summary>任务的整体状态，由取消标记、各工作单元与在跑的 run 汇总得出，不单独维护。</summary>
public enum TaskState
{
    /// <summary>有 run 在跑或排队。</summary>
    Running,

    /// <summary>停在等待用户回答的输入节点上。</summary>
    AwaitingInput,

    /// <summary>停在待批准的执行节点上。</summary>
    AwaitingApproval,

    /// <summary>有单元被阻塞，任务不再自动推进。</summary>
    Blocked,

    /// <summary>全部单元走完流程。</summary>
    Done,

    /// <summary>已取消。</summary>
    Canceled,
}
