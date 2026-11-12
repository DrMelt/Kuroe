using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks;
using Kuroe.Flow.Tasks.Records;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Tasks;

/// <summary>任务控制状态：瞬态 Done 快照不阻止暂停与终止，真正完成后才拒绝。</summary>
public sealed class WorkTaskTests
{
    [Fact]
    public void Pause_accepts_transient_done_snapshot()
    {
        WorkTask task = CreateTask();

        Assert.Equal(TaskState.Done, task.State);
        Assert.False(task.Completed);

        ErrorOr<Success> paused = task.UnderLock(() => task.Pause());

        Assert.False(paused.IsError);
        Assert.Equal(TaskState.Paused, task.State);
        Assert.False(task.Completed);
    }

    [Fact]
    public void Terminate_accepts_transient_done_snapshot()
    {
        WorkTask task = CreateTask();

        Assert.Equal(TaskState.Done, task.State);
        Assert.False(task.Completed);

        ErrorOr<Success> terminated = task.UnderLock(() => task.Terminate());

        Assert.False(terminated.IsError);
        Assert.Equal(TaskState.Terminated, task.State);
        Assert.False(task.Completed);
    }

    [Fact]
    public void Pause_rejects_completed_task()
    {
        WorkTask task = CreateTask();
        MarkCompleted(task);

        ErrorOr<Success> paused = task.UnderLock(() => task.Pause());

        Assert.True(paused.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotPause, paused.FirstError.Code);
    }

    [Fact]
    public void Terminate_rejects_completed_task()
    {
        WorkTask task = CreateTask();
        MarkCompleted(task);

        ErrorOr<Success> terminated = task.UnderLock(() => task.Terminate());

        Assert.True(terminated.IsError);
        Assert.Equal(FlowErrorCodes.TaskDoneCannotTerminate, terminated.FirstError.Code);
    }

    private static WorkTask CreateTask()
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
        TestProvider provider = new();

        return new WorkTask(
            new TaskRegistry(new ExecutionIndex()),
            new TaskId(1),
            inputs,
            FlowTexts.Empty,
            "测试任务",
            provider.Provider,
            new TestState(inputs),
            null,
            observer: null);
    }

    private static void MarkCompleted(WorkTask task) =>
        task.UnderLock(() =>
        {
            task.MarkCompleted();
            return true;
        });

    private sealed class TestState(StartInputs inputs) : IFlowState
    {
        public StartInputs Inputs { get; } = inputs;
    }

    private sealed class TestProvider
    {
        public TestProvider()
        {
            Framework = new TestFramework();
            Presentation = new TestPresentation();
            Provider = new FlowProvider(
                Framework,
                new TestSubmission(),
                new TestControls(),
                Presentation,
                new TestRecords());
        }

        public IFlowFramework Framework { get; }

        public IFlowPresentation Presentation { get; }

        public FlowProvider Provider { get; }
    }

    private sealed class TestFramework : IFlowFramework
    {
        public string Kind => "test";

        public string Name => "测试";

        public bool Handles(FlowName name) => false;

        public bool Handles(Kuroe.Flow.Tasks.Records.FlowRecord record) => false;
    }

    private sealed class TestSubmission : IFlowSubmission
    {
        public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title) =>
            throw new NotSupportedException();

        public ErrorOr<FlowRestore> Restore(TaskRecord record) => throw new NotSupportedException();
    }

    private sealed class TestControls : IFlowControlFactory
    {
        public void Attach(ITaskFlowContext task)
        {
        }

        public IFlowControl CreateControl(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class TestPresentation : IFlowPresentation
    {
        public IFlowView CaptureView(ITaskFlowContext task) => new FlowView("测试", "test", null, [], [], []);

        public TaskSnapshot CreateSnapshot(ITaskFlowContext task) => new(
            task.Id,
            task.Title,
            task.Inputs,
            CaptureView(task),
            task.State,
            0,
            task.JournalEntries,
            task.DroppedJournal,
            task.CreatedAt,
            DateTimeOffset.UtcNow);
    }

    private sealed class TestRecords : IFlowRecordSource
    {
        public FlowRecord CaptureRecord(ITaskFlowContext task) => throw new NotSupportedException();

        public IReadOnlyList<Kuroe.Flow.Execution.Records.ExecutionRecord> CaptureExecutions(
            ITaskFlowContext task) => [];
    }
}