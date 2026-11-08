using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务存档捕获：身份来自任务，调度种子由调用方给出。</summary>
public sealed class TaskRecordCaptureTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(TestFlows.InputFlow, persistTasks: true);

    [Fact]
    public void Capture_uses_supplied_scheduler_seed()
    {
        TaskId id = _environment.Submit("捕获存档");
        _environment.Wait(id, _ => _environment.Awaiting(id));
        WorkTask task = _environment.Registry.Find(id).ThrowIfError();
        NodePath target = NodePath.Of("整体", "用户输入");

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord(new SchedulerSeed([target]))
            .ThrowIfError();

        Assert.Equal(id, record.Identity.Id);
        Assert.Equal([target], record.Pending.Targets);
        Assert.False(record.Completed);
        Assert.NotEmpty(record.Nodes);
    }

    [Fact]
    public void Capture_retains_published_port_history()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        TaskId id = environment.Submit("捕获端口历史");
        environment.Settle(id);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord(new SchedulerSeed([]))
            .ThrowIfError();

        Assert.NotEmpty(record.Ports);
        Assert.Contains(
            record.Nodes.OfType<ExecutableNodeRecord>(),
            node => node.Common.ActivePublicationRevision is not null);
        Assert.All(record.Ports, port => Assert.NotEqual(default, port.At));
    }

    public void Dispose() => _environment.Dispose();
}