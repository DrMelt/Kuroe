using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Execution;
using Kuroe.Flow.Presentation.Shared;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Tasks.Records.Shared;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime;

/// <summary>流程运行期按控制实例生命周期维护活跃索引，不按工作循环退出移除控制实例。</summary>
public sealed class FlowRuntimeTests
{
    [Fact]
    public void Control_stays_active_while_completion_is_pending()
    {
        TestControl control = new();
        WorkTask task = CreateTask(control);
        FlowRuntime runtime = new(Runtime());

        task.UnderLock(() =>
        {
            runtime.Start(task);
            return true;
        });

        Assert.Equal(1, control.Starts);
        Assert.True(control.Loop.IsCompleted);

        task.UnderLock(() =>
        {
            runtime.Pause(task);
            runtime.ResumeTask(task);
            return true;
        });

        Assert.Equal(1, control.Pauses);
        Assert.Equal(1, control.Resumes);
        Assert.Equal(1, runtime.Rerun(task));
    }

    [Fact]
    public async Task Completion_removes_the_control_from_the_active_index()
    {
        TestControl control = new();
        WorkTask task = CreateTask(control);
        FlowRuntime runtime = new(Runtime());

        task.UnderLock(() =>
        {
            runtime.Start(task);
            return true;
        });

        control.Finish();

        await WaitUntil(() => runtime.ActiveControls == 0);

        lock (task.Gate)
        {
            runtime.Pause(task);
        }

        Assert.Equal(0, control.Pauses);
    }

    [Fact]
    public async Task Shutdown_interrupts_controls_and_waits_for_completion()
    {
        TestControl control = new();
        WorkTask task = CreateTask(control);
        FlowRuntime runtime = new(Runtime());

        task.UnderLock(() =>
        {
            runtime.Start(task);
            return true;
        });

        await runtime.ShutdownAsync();

        Assert.Equal(1, control.Shutdowns);
        Assert.True(control.Completion.IsCompleted);
    }

    [Fact]
    public async Task Controls_for_different_tasks_are_independent()
    {
        TestControl firstControl = new();
        TestControl secondControl = new();
        WorkTask first = CreateTask(firstControl);
        WorkTask second = CreateTask(secondControl);
        FlowRuntime runtime = new(Runtime());

        first.UnderLock(() =>
        {
            runtime.Start(first);
            return true;
        });
        second.UnderLock(() =>
        {
            runtime.Start(second);
            return true;
        });

        first.UnderLock(() =>
        {
            runtime.Pause(first);
            return true;
        });

        Assert.Equal(1, firstControl.Pauses);
        Assert.Equal(0, secondControl.Pauses);

        firstControl.Finish();
        await WaitUntil(() =>
        {
            int before = firstControl.Pauses;
            lock (first.Gate)
            {
                runtime.Pause(first);
            }

            return firstControl.Pauses == before;
        });

        second.UnderLock(() =>
        {
            runtime.Pause(second);
            return true;
        });

        Assert.Equal(1, secondControl.Pauses);
    }

    [Fact]
    public void Start_attaches_the_control_before_starting_it()
    {
        TestControl control = new();
        WorkTask task = CreateTask(control);
        FlowRuntime runtime = new(Runtime());

        task.UnderLock(() =>
        {
            runtime.Start(task);
            return true;
        });

        Assert.True(control.ControlVisibleAtStart);
        Assert.Same(control, task.Control);
    }

    [Fact]
    public void Snapshot_during_start_reads_the_attached_control()
    {
        TestControl control = new();
        WorkTask task = CreateTask(control);
        FlowRuntime runtime = new(Runtime());

        task.UnderLock(() =>
        {
            runtime.Start(task);
            return true;
        });

        Assert.Equal(TaskState.Running, task.Snapshot().State);
    }

    private static WorkTask CreateTask(TestControl control) => FlowRuntimeTestDoubles.CreateTask(control);

    private static IExecutionRuntime Runtime() => FlowRuntimeTestDoubles.Runtime();

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.Fail("等待超时。");
    }
}
internal static class FlowRuntimeTestDoubles
{
    internal static WorkTask CreateTask(TestControl control)
    {
        StartInputs inputs = StartInputs.Single(new PortName("目标"), "目标");
        FlowProvider provider = new(
            new TestIdentity(),
            new TestSubmission(),
            NoFlowStateBinder.Instance,
            new TestControls(control),
            new TestPresentation(),
            new TestRecords(),
            NoFlowTemplates.Instance);

        WorkTask task = new(
            new TaskRegistry(new ExecutionIndex()),
            Runtime(),
            new TaskId(1),
            inputs,
            FlowTexts.Empty,
            "测试任务",
            provider,
            new TestState(inputs),
            null,
            observer: null);
        control.Owner = task;

        return task;
    }

    internal static IExecutionRuntime Runtime() =>
        new ExecutionRuntime(
            new ExecutionDispatcher(),
            new ExecutionRunnerRegistry(),
            new ExecutionRestorerRegistry());
}

internal sealed class TestControl : IFlowControl
{
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ITaskFlowContext? Owner { get; set; }

    public bool ControlVisibleAtStart { get; private set; }

    public int Starts { get; private set; }

    public int Pauses { get; private set; }

    public int Resumes { get; private set; }

    public int Shutdowns { get; private set; }

    public Task Loop => Task.CompletedTask;

    public Task Completion => _completion.Task;

    public bool HasBlocked => false;

    public bool HasWork => true;

    public bool HasRestores => false;

    public void Start()
    {
        Starts++;
        ControlVisibleAtStart = Owner?.Control is not null;
    }

    public void Restore(FlowSeed seed)
    {
    }

    public void Pause() => Pauses++;

    public void Advance(IReadOnlyList<FlowStepPath> steps)
    {
    }

    public void Resume(bool continueRestores = true) => Resumes++;

    public int Rerun() => 1;

    public ErrorOr<Success> Terminate() => Result.Success;

    public FlowSeed CaptureSeed() => FlowSeed.Empty;

    public void InterruptForShutdown()
    {
        Shutdowns++;
        Finish();
    }

    public void Finish() => _completion.TrySetResult();
}
internal sealed class TestState(StartInputs inputs) : IFlowState
{
    public StartInputs Inputs { get; } = inputs;
}

internal sealed class TestIdentity : IFlowIdentity
{
    public string DisplayName => "测试";



    public bool Handles(FlowName name) => false;

    public bool Handles(FlowIdentity identity) => false;
}

internal sealed class TestSubmission : IFlowSubmission
{
    public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title) =>
        throw new NotSupportedException();

    public ErrorOr<FlowRestore> Restore(TaskRecord record) => throw new NotSupportedException();
}

internal sealed class TestControls(IFlowControl control) : IFlowControlFactory
{
    public IFlowControl CreateControl(ITaskFlowContext task) => control;
}

internal sealed class TestPresentation : IFlowPresentation
{
    public IFlowView CaptureView(ITaskFlowContext task) =>
        new FlowView("测试", "test", null, [], [], []);

    public IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task) => [];
}

internal sealed class TestRecords : IFlowRecordSource
{
    public FlowRecord CaptureRecord(ITaskFlowContext task) => throw new NotSupportedException();
}