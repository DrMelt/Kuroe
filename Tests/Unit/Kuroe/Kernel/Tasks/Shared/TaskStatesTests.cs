using Xunit;

namespace Kuroe.Kernel.Tasks.Shared.Tests;

/// <summary>任务状态的判定与展示名。</summary>
public sealed class TaskStatesTests
{
    [Fact(DisplayName = "暂停任务未收口")]
    public void Paused_task_is_not_settled()
    {
        Assert.False(TaskState.Paused.IsSettled());
        Assert.Equal("已暂停", TaskState.Paused.Label());
    }

    [Fact(DisplayName = "终止任务已收口")]
    public void Terminated_task_is_settled()
    {
        Assert.True(TaskState.Terminated.IsSettled());
        Assert.Equal("已终止", TaskState.Terminated.Label());
    }

    [Fact(DisplayName = "完成任务已收口")]
    public void Done_task_is_settled()
    {
        Assert.True(TaskState.Done.IsSettled());
        Assert.Equal("已完成", TaskState.Done.Label());
    }
}
