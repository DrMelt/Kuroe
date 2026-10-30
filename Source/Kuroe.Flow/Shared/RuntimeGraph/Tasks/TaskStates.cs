namespace Kuroe.Flow.Shared.RuntimeGraph.Tasks;

/// <summary>任务状态的判定与展示名。</summary>
public static class TaskStates
{
    /// <summary>已走完或已终止，两者都不再自动推进。</summary>
    public static bool IsSettled(this TaskState state) => state is TaskState.Done or TaskState.Terminated;

    /// <summary>状态的展示名。</summary>
    public static string Label(this TaskState state) => state switch
    {
        TaskState.Running => "执行中",
        TaskState.Blocked => "已阻塞",
        TaskState.Paused => "已暂停",
        TaskState.Done => "已完成",
        TaskState.Terminated => "已终止",
        _ => state.ToString(),
    };
}
