namespace Kuroe.Shared.Workflows;

/// <summary>任务的整体状态，由取消标记、各工作单元与在跑的 agent 汇总得出，不单独维护。</summary>
public enum TaskState
{
    /// <summary>有 agent 在跑或排队。</summary>
    Running,

    /// <summary>停在等人批准的叶子上。</summary>
    AwaitingApproval,

    /// <summary>有单元被阻塞，任务不再自动推进。</summary>
    Blocked,

    /// <summary>全部单元走完流程。</summary>
    Done,

    /// <summary>已取消。</summary>
    Canceled,
}
