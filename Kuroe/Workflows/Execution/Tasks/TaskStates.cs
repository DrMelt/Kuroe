using Kuroe.Shared.Workflows;

namespace Kuroe.Workflows.Execution.Tasks;

/// <summary>任务状态的判定与展示名。</summary>
public static class TaskStates
{
    /// <summary>已走完或已取消，两者都不再自动推进。</summary>
    public static bool IsSettled(this TaskState state) => state is TaskState.Done or TaskState.Canceled;

    /// <summary>状态的展示名。</summary>
    public static string Label(this TaskState state) => state switch
    {
        TaskState.Running => "执行中",
        TaskState.AwaitingInput => "待输入",
        TaskState.AwaitingApproval => "待批准",
        TaskState.Blocked => "已阻塞",
        TaskState.Done => "已完成",
        TaskState.Canceled => "已取消",
        _ => state.ToString(),
    };
}
