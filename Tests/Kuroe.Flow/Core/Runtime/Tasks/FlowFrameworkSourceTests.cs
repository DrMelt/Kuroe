using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Presentation;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Tasks.Records;
using Xunit;

namespace Kuroe.Flow.Tests.Runtime.Tasks;

/// <summary>流程实现分派：恢复按存档定义选择实现，同一种类可以有多个实现。</summary>
public sealed class FlowFrameworkSourceTests
{
    [Fact]
    public void Record_finds_provider_by_definition_implementation()
    {
        FlowProvider first = Provider("first");
        FlowProvider second = Provider("second");
        FlowFrameworkSource source = new([first, second]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("second"));

        Assert.False(found.IsError);
        Assert.Same(second, found.Value);
    }

    [Fact]
    public void Unknown_implementation_returns_an_error()
    {
        FlowFrameworkSource source = new([Provider("first")]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("missing"));

        Assert.True(found.IsError);
    }

    [Fact]
    public void Several_providers_for_the_same_record_are_rejected()
    {
        FlowFrameworkSource source = new([Provider("same"), Provider("same")]);

        ErrorOr<FlowProvider> found = source.Find(CodeRecord("same"));

        Assert.True(found.IsError);
    }

    [Fact]
    public void Restore_uses_the_provider_selected_by_the_record()
    {
        FlowProvider first = Provider("first");
        FlowProvider second = Provider("second");
        ExecutionIndex executions = new();
        TaskRegistry registry = new(executions);
        TaskRestoreRuntime runtime = new(registry, new FlowFrameworkSource([first, second]), new UnusedRuntime());

        TaskSnapshot snapshot = runtime.Restore(CodeTask("second")).ThrowIfError();

        Assert.Equal("second", snapshot.View.Name);
    }

    private static FlowProvider Provider(string implementation)
    {
        TestFramework framework = new(implementation);
        return new FlowProvider(
            framework,
            new TestSubmission(),
            new TestControls(),
            new TestPresentation(implementation),
            new TestRecords());
    }

    private static FlowRecord CodeRecord(string implementation) =>
        new(
            FlowRecordKinds.Code,
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

    private sealed class TestFramework(string implementation) : IFlowFramework
    {
        public string Kind => FlowRecordKinds.Code;

        public string Name => implementation;

        public bool Handles(FlowName name) => false;

        public bool Handles(FlowRecord record) =>
            record.Kind == Kind
            && record.Definition is CodeFlowDefinitionRecord definition
            && definition.Implementation == implementation;
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
        public void Attach(ITaskFlowContext task)
        {
        }

        public IFlowControl CreateControl(ITaskFlowContext task) => throw new NotSupportedException();
    }

    private sealed class TestPresentation(string name) : IFlowPresentation
    {
        public IFlowView CaptureView(ITaskFlowContext task) => new FlowView(name, "code", null, [], [], []);

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
            task.LastActivityAt);
    }

    private sealed class TestRecords : IFlowRecordSource
    {
        public FlowRecord CaptureRecord(ITaskFlowContext task) => throw new NotSupportedException();

        public IReadOnlyList<Kuroe.Flow.Execution.Records.ExecutionRecord> CaptureExecutions(
            ITaskFlowContext task) => [];
    }

    private sealed class UnusedRuntime : IFlowRuntime
    {
        public void Start(ITaskFlowHost task) => throw new NotSupportedException();

        public void Restore(ITaskFlowHost task, FlowSeed seed)
        {
        }

        public void Pause(ITaskFlowContext task) => throw new NotSupportedException();

        public ErrorOr<Success> StopExecution(ITaskFlowContext task, Kuroe.Flow.Execution.ExecutionId id) =>
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