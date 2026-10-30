using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Tests.Persistence;

/// <summary>任务检查点捕获：身份来自任务，调度种子由调用方给出。</summary>
public sealed class TaskCheckpointCaptureTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(TestFlows.InputFlow, persistTasks: true);

    [Fact]
    public void Capture_uses_supplied_scheduler_seed()
    {
        TaskId id = _environment.Submit("捕获检查点");
        _environment.Settle(id);
        WorkTask task = _environment.Registry.Find(id).ThrowIfError();
        NodePath target = NodePath.Of("整体", "用户输入");

        TaskCheckpoint checkpoint = new TaskCheckpointCapture(task)
            .CaptureCheckpoint(new SchedulerSeed([target]))
            .ThrowIfError();

        Assert.Equal(id, checkpoint.Identity.Id);
        Assert.Equal([target], checkpoint.Pending.Targets);
        Assert.False(checkpoint.Completed);
        Assert.NotEmpty(checkpoint.Nodes);
    }

    public void Dispose() => _environment.Dispose();
}