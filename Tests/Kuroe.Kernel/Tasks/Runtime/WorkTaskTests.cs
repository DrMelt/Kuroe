using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Input.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Control.Runtime;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Tasks.Runtime.Tests;

/// <summary>任务控制状态：瞬态 Done 快照不阻止暂停与终止，真正完成后才拒绝。</summary>
public sealed class WorkTaskTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Execution.ExecutionRuntime Runtime() =>
        new(new Kuroe.Execution.ExecutionDispatcher());
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

    [Fact]
    public void Construction_binds_state_once()
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
        TestProvider provider = new();

        _ = new WorkTask(
            new TaskRegistry(new ExecutionIndex()),
            Runtime(),
            new TaskId(1),
            inputs,
            FlowTexts.Empty,
            "测试任务",
            provider.Provider,
            new TestState(inputs),
            null,
            observer: null);

        Assert.Equal(1, provider.Binder.Binds);
    }

    [Fact]
    public void ClearSettled_removes_input_registration()
    {
        FakeInputDelivery inputs = new([]);
        TaskRegistry registry = new(new ExecutionIndex(), inputs);
        WorkTask task = CreateTask(registry);
        registry.Adopt(task);

        IReadOnlyList<TaskId> cleared = registry.ClearSettled();

        Assert.Equal([task.Id], cleared);
        Assert.Equal([task.Id], inputs.Removed);
    }

    [Fact]
    public void Snapshot_assembles_common_fields_from_task_and_presentation()
    {
        FlowPendingStep pending = new(FlowStepPath.Of("等待"), FlowPendingKind.Approve, "确认");
        FakeInputDelivery inputs = new([pending]);
        WorkTask task = CreateTask(new TaskRegistry(new ExecutionIndex(), inputs));
        IFlowView view = new FlowView("测试", "test", null, [], [], []);
        FlowTexts startTexts = new(new Dictionary<PortName, string> { [new PortName("目标")] = "目标" });

        TaskSnapshot snapshot = TaskSnapshotFactory.Create(task, view, startTexts);

        Assert.Equal(task.Id, snapshot.Id);
        Assert.Equal(task.Title, snapshot.Title);
        Assert.Equal(task.Inputs, snapshot.Inputs);
        Assert.Same(view, snapshot.View);
        Assert.Equal(task.State, snapshot.State);
        Assert.Equal(task.JournalEntries, snapshot.Journal);
        Assert.Equal(task.CreatedAt, snapshot.CreatedAt);
        Assert.Equal(task.LastActivityAt, snapshot.LastActivityAt);
        Assert.Same(startTexts, snapshot.StartTexts);
        PendingStepView step = Assert.Single(snapshot.Pending);
        Assert.Equal("等待", step.Path);
        Assert.Equal(PendingStepKind.Approve, step.Kind);
        Assert.Equal("确认", step.Question);
    }

    private static WorkTask CreateTask(TaskRegistry? registry = null)
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
        TestProvider provider = new();

        return new WorkTask(
            registry ?? new TaskRegistry(new ExecutionIndex()),
            Runtime(),
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
            ((ITaskFlowContext)task).MarkCompleted();
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
            Identity = new TestIdentity();
            Presentation = new TestPresentation();
            Binder = new TestBinder();
            Provider = new FlowProvider(
                Identity,
                new TestSubmission(),
                Binder,
                new TestControls(),
                Presentation,
                new TestRecords());
        }

        public IFlowIdentity Identity { get; }

        public IFlowPresentation Presentation { get; }

        public TestBinder Binder { get; }

        public FlowProvider Provider { get; }
    }

    private sealed class TestIdentity : IFlowIdentity
    {
        public string DisplayName => "测试";



        public bool Handles(FlowName name) => false;

        public bool Handles(FlowIdentity identity) => false;
    }

    private sealed class TestSubmission : IFlowSubmission
    {
        public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title) =>
            throw new NotSupportedException();

        public ErrorOr<FlowRestore> Restore(TaskRecord record) => throw new NotSupportedException();
    }

    private sealed class TestBinder : IFlowStateBinder
    {
        public int Binds { get; private set; }

        public void Bind(ITaskFlowContext task) => Binds++;
    }

    private sealed class TestControls : IFlowControlFactory
    {
        public IFlowControl CreateControl(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class TestPresentation : IFlowPresentation
    {
        public IFlowView CaptureView(ITaskFlowView task) => new FlowView("测试", "test", null, [], [], []);
    }

    private sealed class TestRecords : IFlowRecordSource
    {
        public FlowRecord CaptureRecord(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class FakeInputDelivery(IReadOnlyList<FlowPendingStep> pending) : IInputDelivery
    {
        public List<TaskId> Removed { get; } = [];

        public IReadOnlyList<FlowPendingStep> Pending(TaskId task) => pending;

        public ErrorOr<Success> Deliver(TaskId task, FlowStepPath step, string input) => Result.Success;

        public void Remove(TaskId task) => Removed.Add(task);
    }
}