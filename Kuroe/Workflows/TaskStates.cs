using Kuroe.Shared.Workflows;

namespace Kuroe.Workflows;

/// <summary>任务是否已收口，收口后不再自动推进。</summary>
public static class TaskStates
{
    /// <summary>已走完或已取消，两者都不再自动推进。</summary>
    public static bool IsSettled(this TaskState state) => state is TaskState.Done or TaskState.Canceled;
}