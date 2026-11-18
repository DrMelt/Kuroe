using Kuroe.Flow.Graph.Records.Shared;
using Kuroe.Flow.Node.Shared.Graph.Records;
using Kuroe.Tasks.Runtime;
using Kuroe.Tasks.Runtime.Persistence;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务存档捕获：身份来自任务，调度种子由调用方给出。</summary>
public sealed class TaskRecordCaptureTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(TestFlows.InputFlow, persistTasks: true);

    [Fact(DisplayName = "存档捕获使用待处理步骤")]
    public void Capture_uses_the_pending_step()
    {
        TaskId id = _environment.Submit("捕获存档");
        _environment.Wait(id, _ => _environment.Awaiting(id));
        WorkTask task = _environment.Registry.Find(id).ThrowIfError();

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord()
            .ThrowIfError();

        Assert.Equal(id, record.Identity.Id);
        Assert.NotEmpty(State(record).Nodes);
    }

    [Fact(DisplayName = "存档捕获保留已发布端口历史")]
    public void Capture_retains_published_port_history()
    {
        using TestEnvironment environment = TestEnvironment.Create(persistTasks: true);
        TaskId id = environment.Submit("捕获端口历史");
        environment.Settle(id);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord()
            .ThrowIfError();

        Assert.NotEmpty(State(record).Ports);
        Assert.Contains(
            State(record).Nodes.OfType<ExecutableNodeRecord>(),
            node => node.Common.ActivePublicationRevision is not null);
        Assert.All(State(record).Ports, port => Assert.NotEqual(default, port.At));
    }

    [Fact(DisplayName = "存档捕获保留成功执行的恢复数据")]
    public void Capture_retains_restore_data_of_a_successful_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow, persistTasks: true);
        environment.Runner.RestoreData = new Kuroe.Execution.Shared.Restore.ExecutionRestoreState("test", "data");
        TaskId id = environment.Submit("保留恢复数据");
        environment.Settle(id);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord()
            .ThrowIfError();

        Assert.Contains(
            record.Executions,
            execution => execution.State == Kuroe.Execution.Shared.State.ExecutionState.Succeeded && execution.RestoreData is not null);
    }

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);

    public void Dispose() => _environment.Dispose();
}