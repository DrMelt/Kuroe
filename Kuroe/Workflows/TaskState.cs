namespace Kuroe.Workflows;

/// <summary>任务的整体状态，由取消标记、各工作单元与在跑的 agent 汇总得出，不单独维护。</summary>
public enum TaskState
{
    /// <summary>有 agent 在跑或排队。</summary>
    Running,

    /// <summary>停在等人批准的步骤上。</summary>
    AwaitingApproval,

    /// <summary>有单元被阻塞，任务不再自动推进。</summary>
    Blocked,

    /// <summary>全部单元走完流程。</summary>
    Done,

    /// <summary>已取消。</summary>
    Canceled,
}

/// <summary>任务是否已收口，收口后不再自动推进。</summary>
public static class TaskStates
{
    /// <summary>已走完或已取消，两者都不再自动推进。</summary>
    public static bool IsSettled(this TaskState state) => state is TaskState.Done or TaskState.Canceled;
}
