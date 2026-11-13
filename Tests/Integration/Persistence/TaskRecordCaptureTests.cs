using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Execution.Records;
using Kuroe.Flow.Graph.Records;
using Kuroe.Flow.Tasks.Records;
using Kuroe.Flow.Node.RuntimeGraph.Execution;
using Kuroe.Flow.Node.RuntimeGraph.Tasks;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务存档捕获：身份来自任务，调度种子由调用方给出。</summary>
public sealed class TaskRecordCaptureTests : IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create(TestFlows.InputFlow, persistTasks: true);

    [Fact]
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

    [Fact]
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

    [Fact]
    public void Capture_retains_restore_data_of_a_successful_execution()
    {
        using TestEnvironment environment = TestEnvironment.Create(TestFlows.PortFlow, persistTasks: true);
        environment.Runner.RestoreData = new Kuroe.Flow.Execution.ExecutionRestoreState("test", "data");
        TaskId id = environment.Submit("保留恢复数据");
        environment.Settle(id);
        WorkTask task = environment.Registry.Find(id).ThrowIfError();

        TaskRecord record = new TaskRecordCapture(task)
            .CaptureRecord()
            .ThrowIfError();

        Assert.Contains(
            record.Executions,
            execution => execution.State == Kuroe.Flow.Execution.ExecutionState.Succeeded && execution.RestoreData is not null);
    }

    private static GraphFlowStateRecord State(TaskRecord record) =>
        Assert.IsType<GraphFlowStateRecord>(record.Flow.State);

    public void Dispose() => _environment.Dispose();
}