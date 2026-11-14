using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Flow.Code.Records.Shared;
using Kuroe.Presentation.Shared;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Tasks.Presentation.Shared;
using Kuroe.Tasks.Records.Shared;
using Kuroe.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Tasks;

/// <summary>流程实现分派：恢复按存档定义选择实现，同一种类可以有多个实现。</summary>
public sealed class FlowProviderSourceTests
{
    /// <summary>测试用的执行核，派发器不启动后台任务。</summary>
    private static Kuroe.Execution.ExecutionRuntime Runtime() =>
        new(
            new Kuroe.Execution.ExecutionDispatcher(),
            new Kuroe.Execution.ExecutionRunnerRegistry(),
            new Kuroe.Execution.ExecutionRestorerRegistry());
    [Fact]
    public void Identity_finds_provider_by_implementation()
    {
        FlowProvider first = Provider("first");
        FlowProvider second = Provider("second");
        FlowProviderSource source = new([first, second]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("second").Identity);

        Assert.False(found.IsError);
        Assert.Same(second, found.Value);
    }

    [Fact]
    public void Unknown_implementation_returns_an_error()
    {
        FlowProviderSource source = new([Provider("first")]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("missing").Identity);

        Assert.True(found.IsError);
    }

    [Fact]
    public void Several_providers_for_the_same_identity_are_rejected()
    {
        FlowProviderSource source = new([Provider("same"), Provider("same")]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("same").Identity);

        Assert.True(found.IsError);
    }

    [Fact]
    public void Name_resolves_the_provider_that_owns_it()
    {
        FlowProvider first = Provider("first");
        FlowProvider second = Provider("second");
        FlowProviderSource source = new([first, second]);

        ErrorOr<FlowProvider> found = source.Resolve(new FlowName("second"));

        Assert.Same(second, found.Value);
    }

    [Fact]
    public void Restore_uses_the_provider_selected_by_the_identity()
    {
        FlowProvider first = Provider("first");
        FlowProvider second = Provider("second");
        ExecutionIndex executions = new();
        TaskRegistry registry = new(executions);
        TaskRestoreRuntime runtime = new(
            registry,
            Runtime(),
            new FlowProviderSource([first, second]),
            new UnusedRuntime());

        TaskSnapshot snapshot = runtime.Restore(CodeTask("second")).ThrowIfError();

        Assert.Equal("second", snapshot.View.Name);
    }

    private static FlowProvider Provider(string implementation)
    {
        TestIdentity identity = new(implementation);
        return new FlowProvider(
            identity,
            new TestSubmission(),
            NoFlowStateBinder.Instance,
            new TestControls(),
            new TestPresentation(implementation),
            new TestRecords(),
            NoFlowTemplates.Instance);
    }

    private static FlowRecord CodeRecord(string implementation) =>
        new(
            new FlowIdentity(new FlowName(implementation), CodeFlowKind.Value, implementation),
            new CodeFlowDefinitionRecord(implementation, new Dictionary<string, string>()),
            new CodeFlowStateRecord(
                new StartInputs([]),
                FlowTexts.Empty,
                new Dictionary<string, string>()));

    private static TaskRecord CodeTask(string implementation)
    {
        DateTimeOffset at = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        return new TaskRecord(
            new TaskIdentity(new TaskId(11), "代码任务", at, at),
            at,
            TaskLifecycle.Active,
            Completed: false,
            new TaskJournalRecord([], 0),
            CodeRecord(implementation),
            []);
    }

    private sealed class TestIdentity(string implementation) : IFlowIdentity
    {
        public string DisplayName => implementation;

        public bool Handles(FlowName name) => name.Value == implementation;

        public bool Handles(FlowIdentity identity) =>
            identity.Kind == CodeFlowKind.Value
            && identity.Implementation == implementation;
    }

    private sealed class TestSubmission : IFlowSubmission
    {
        public ErrorOr<FlowSubmission> Prepare(TaskId id, FlowName name, StartInputs inputs, string? title) =>
            throw new NotSupportedException();

        public ErrorOr<FlowRestore> Restore(TaskRecord record) =>
            new FlowRestore(
                new TestState(record.Flow.State is CodeFlowStateRecord state ? state.Inputs : new StartInputs([])),
                FlowTexts.Empty,
                [],
                FlowSeed.Empty);
    }

    private sealed class TestState(StartInputs inputs) : IFlowState
    {
        public StartInputs Inputs { get; } = inputs;
    }

    private sealed class TestControls : IFlowControlFactory
    {
        public IFlowControl CreateControl(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class TestPresentation(string name) : IFlowPresentation
    {
        public IFlowView CaptureView(ITaskFlowContext task) => new FlowView(name, "code", null, [], [], []);

        public IReadOnlyList<FlowPendingStep> Pending(ITaskFlowContext task) => [];
    }

    private sealed class TestRecords : IFlowRecordSource
    {
        public FlowRecord CaptureRecord(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class UnusedRuntime : IFlowRuntime
    {
        public void Start(ITaskFlowHost task) => throw new NotSupportedException();

        public void Restore(ITaskFlowHost task, FlowSeed seed)
        {
        }

        public void Pause(ITaskFlowContext task) => throw new NotSupportedException();

        public ErrorOr<Success> StopExecution(ITaskFlowContext task, Kuroe.Execution.Shared.ExecutionId id) =>
            throw new NotSupportedException();

        public void ResumeTask(ITaskFlowContext task, bool continueRestores = true) =>
            throw new NotSupportedException();

        public void Advance(ITaskFlowContext task, IReadOnlyList<FlowStepPath> steps) =>
            throw new NotSupportedException();

        public int Rerun(ITaskFlowContext task) => throw new NotSupportedException();

        public ErrorOr<Success> Terminate(ITaskFlowContext task) => throw new NotSupportedException();

        public Task ShutdownAsync() => Task.CompletedTask;
    }
}