using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Flow.Tests;

/// <summary>任务状态的判定与展示名。</summary>
public sealed class TaskStatesTests
{
    [Fact]
    public void Paused_task_is_not_settled()
    {
        Assert.False(TaskState.Paused.IsSettled());
        Assert.Equal("已暂停", TaskState.Paused.Label());
    }

    [Fact]
    public void Terminated_task_is_settled()
    {
        Assert.True(TaskState.Terminated.IsSettled());
        Assert.Equal("已终止", TaskState.Terminated.Label());
    }

    [Fact]
    public void Done_task_is_settled()
    {
        Assert.True(TaskState.Done.IsSettled());
        Assert.Equal("已完成", TaskState.Done.Label());
    }
}
