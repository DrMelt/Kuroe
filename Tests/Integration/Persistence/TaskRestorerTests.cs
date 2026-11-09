using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Engine;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Records.Shared;
using Kuroe.Flow.Shared.RuntimeGraph;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Nodes;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.Shared.StaticGraph.Graph;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务恢复契约的实现：用默认测试流程验证完成、未完成与中断三种存档。</summary>
public sealed class TaskRestorerTests : TaskRestoreContractTests, IDisposable
{
    private readonly TestEnvironment _environment = TestEnvironment.Create();
    private readonly ExecutionIndex _executions = new();
    private readonly TaskRegistry _registry;

    public TaskRestorerTests() => _registry = new TaskRegistry(_executions);

    protected override ITaskRestorer CreateRestorer() => new TaskRestorer(_registry);

    protected override CompiledFlow CompileFixtureFlow()
    {
        ErrorOr<CompiledFlow> flow = _environment.Flows.FindCompiled(new FlowName("默认"));

        return flow.IsError
            ? throw new InvalidOperationException(string.Join("；", flow.ErrorsOrEmptyList.Select(error => error.Description)))
            : flow.Value;
    }

    protected override TaskRecord DoneRecord => Create(TaskLifecycle.Active, ExecutionState.Succeeded, published: true, blocked: false) with { Completed = true };

    protected override TaskRecord UnfinishedRecord => Create(TaskLifecycle.Paused, ExecutionState.Succeeded, published: false, blocked: false);

    protected override TaskRecord InterruptedRecord => Create(TaskLifecycle.Paused, ExecutionState.Interrupted, published: false, blocked: true);

    [Fact]
    public void Restore_does_not_block_an_execution_that_never_started()
    {
        TaskRecord record = InterruptedRecord with
        {
            Executions = [InterruptedRecord.Executions[0] with { StartedAt = null }],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        Assert.DoesNotContain(
            restored.Value.Snapshot().ExecutableStates,
            state => state.State == NodeState.Blocked);
    }

    [Fact]
    public void Restore_preserves_restore_data()
    {
        _registry.RestorerSource = new AcceptAllRestorer();
        ExecutionRestorePayload data = new("test", "state");
        TaskRecord record = InterruptedRecord with
        {
            Executions =
            [
                InterruptedRecord.Executions[0] with
                {
                    RestoreData = data,
                    ContinuedFrom = new ExecutionId(9),
                },
            ],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        ExecutionSnapshot execution = restored.Value.Executions[0].Snapshot();
        Assert.True(execution.HasRestoreData);
        Assert.Equal(new ExecutionId(9), execution.ContinuedFrom);

        TaskSnapshot snapshot = restored.Value.Snapshot();
        Assert.True(Assert.Single(snapshot.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);
    }

    [Fact]
    public void Restore_does_not_plan_a_block_without_a_resumable_execution()
    {
        _registry.RestorerSource = new AcceptAllRestorer();
        ExecutableNodeRecord blocked = Assert.IsType<ExecutableNodeRecord>(InterruptedRecord.Nodes[0]);
        TaskRecord record = InterruptedRecord with
        {
            Executions =
            [
                InterruptedRecord.Executions[0] with { RestoreData = new ExecutionRestorePayload("test", "state") },
            ],
            Nodes =
            [
                blocked with { Block = blocked.Block is { } block ? block with { Execution = null } : null },
                .. InterruptedRecord.Nodes.Skip(1),
            ],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        TaskSnapshot snapshot = restored.Value.Snapshot();
        Assert.False(Assert.Single(snapshot.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);
    }

    [Fact]
    public void Restore_keeps_restore_data_of_a_finished_execution()
    {
        ExecutionRestorePayload data = new("test", "state");
        TaskRecord record = DoneRecord with
        {
            Executions = [DoneRecord.Executions[0] with { RestoreData = data }],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        ExecutionSnapshot execution = restored.Value.Executions[0].Snapshot();
        Assert.True(execution.HasRestoreData);
    }

    [Fact]
    public void Restore_terminated_record_cancels_nodes_and_clears_blocks()
    {
        _registry.RestorerSource = new AcceptAllRestorer();
        TaskRecord template = Create(TaskLifecycle.Terminated, ExecutionState.Interrupted, published: false, blocked: true);
        TaskRecord record = template with
        {
            Executions =
            [
                template.Executions[0] with { RestoreData = new ExecutionRestorePayload("test", "state") },
            ],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        TaskSnapshot snapshot = restored.Value.Snapshot();
        Assert.Equal(TaskState.Terminated, snapshot.State);
        Assert.Contains(snapshot.ExecutableStates, state => state.State == NodeState.Canceled);
        Assert.DoesNotContain(snapshot.ExecutableStates, state => state.Block is not null);
        Assert.DoesNotContain(snapshot.ExecutableStates, state => state.CanRestore);
        Assert.True(restored.Value.Executions[0].Snapshot().HasRestoreData);
    }

    [Fact]
    public void Restore_does_not_plan_a_block_associated_with_a_succeeded_execution()
    {
        _registry.RestorerSource = new AcceptAllRestorer();
        TaskRecord template = Create(TaskLifecycle.Paused, ExecutionState.Succeeded, published: false, blocked: true);
        TaskRecord record = template with
        {
            Executions =
            [
                template.Executions[0] with { RestoreData = new ExecutionRestorePayload("test", "state") },
            ],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        ExecutableStateSnapshot blocked = Assert.Single(
            restored.Value.Snapshot().ExecutableStates,
            state => state.State == NodeState.Blocked);
        Assert.False(blocked.CanRestore);
    }

    [Fact]
    public void Restore_does_not_plan_a_block_of_a_completed_task()
    {
        _registry.RestorerSource = new AcceptAllRestorer();
        TaskRecord template = Create(TaskLifecycle.Active, ExecutionState.Failed, published: true, blocked: true);
        TaskRecord record = template with
        {
            Completed = true,
            Executions =
            [
                template.Executions[0] with { RestoreData = new ExecutionRestorePayload("test", "state") },
            ],
        };

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        TaskSnapshot snapshot = restored.Value.Snapshot();
        Assert.Equal(TaskState.Done, snapshot.State);
        Assert.False(Assert.Single(snapshot.ExecutableStates, state => state.State == NodeState.Blocked).CanRestore);
    }

    [Fact]
    public void Restore_runtime_does_not_resume_a_terminated_task()
    {
        TaskRecord record = Create(TaskLifecycle.Terminated, ExecutionState.Interrupted, published: false, blocked: true);
        RecordingRunner runner = new();
        TaskRestoreRuntime runtime = new(_registry, CreateRestorer(), runner, new AcceptAllRestorer());

        ErrorOr<TaskSnapshot> restored = runtime.Restore(record, CompileFixtureFlow(), []);

        Assert.False(restored.IsError);
        Assert.Equal(TaskState.Terminated, restored.Value.State);
        Assert.Equal(0, runner.RestoreCount);
    }

    [Fact]
    public void Restore_runtime_resumes_a_paused_task()
    {
        TaskRecord template = Create(TaskLifecycle.Paused, ExecutionState.Interrupted, published: false, blocked: true);
        TaskRecord record = template with
        {
            Executions = [template.Executions[0] with { StartedAt = null }],
        };
        RecordingRunner runner = new();
        TaskRestoreRuntime runtime = new(_registry, CreateRestorer(), runner, new AcceptAllRestorer());

        ErrorOr<TaskSnapshot> restored = runtime.Restore(record, CompileFixtureFlow(), []);

        Assert.False(restored.IsError);
        Assert.Equal(TaskState.Paused, restored.Value.State);
        Assert.Equal(1, runner.RestoreCount);
        Assert.Contains(NodePath.Of("整体", "制定计划"), runner.Seed);
    }

    [Fact]
    public void Restore_keeps_the_task_record_and_leaves_execution_process_records_empty()
    {
        ErrorOr<WorkTask> restored = CreateRestorer().Restore(DoneRecord, CompileFixtureFlow());

        Assert.False(restored.IsError);
        Assert.NotEmpty(restored.Value.Snapshot().Journal);
        Assert.All(restored.Value.Executions, execution => Assert.Empty(execution.Snapshot().Journal));
    }

    [Fact]
    public void Restore_registers_executions_in_shared_index()
    {
        TaskRecord record = UnfinishedRecord;

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.False(restored.IsError);
        Assert.False(_registry.FindExecution(record.Executions[0].Id).IsError);
    }

    private static TaskRecord Create(
        TaskLifecycle lifecycle,
        ExecutionState state,
        bool published,
        bool blocked)
    {
        TaskId id = new(1);
        NodePath plan = NodePath.Of("整体", "制定计划");
        NodePath container = NodePath.Of("整体");
        ExecutionId execution = new(1);
        DateTimeOffset at = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        PortName target = new("目标");
        PortName result = new("计划");

        return new TaskRecord(
            new TaskIdentity(id, "恢复任务", at, at),
            at,
            new FlowSnapshot(new FlowName("默认"), """{"Nodes":[]}""", """{"Flows":[]}"""),
            new TaskStartSnapshot(
                StartInputs.Single(target, "目标内容"),
                new Dictionary<PortName, string> { [target] = "目标内容" }),
            lifecycle,
            Completed: false,
            new TaskJournalSnapshot([], 0),
            [
                new ExecutionRecord(
                    execution,
                    plan,
                    1,
                    state,
                    string.Empty,
                    at,
                    at,
                    at,
                    state == ExecutionState.Interrupted ? ["任务已暂停。"] : [],
                    ExecutionUsage.None,
                    [new InputValueRecord(target, new ContextSourceRecord(ContextSourceKind.Start, null, null, target), "目标内容")],
                    new Dictionary<PortName, string> { [target] = "目标内容" }),
            ],
            [
                new ExecutableNodeRecord(
                    plan,
                    Common(
                        published ? 1 : 0,
                        published ? 1 : null),
                    blocked ? [plan] : [],
                    blocked ? new NodeBlock("任务已暂停。", execution, at) : null),
                new ContainerNodeRecord(container, Common(published ? 1 : 0)),
            ],
            published ? [PublishedRecord(execution, plan, result)] : [],
            new SchedulerSeed([]));
    }

    private static NodeCommonRecord Common(long revision, long? activePublicationRevision = null) =>
        new(
            revision,
            1,
            null,
            [new ConsumedRevisionRecord(NodePath.Of("整体"), new PortName("目标"), 1)],
            activePublicationRevision,
            false);

    private static PortPublicationRecord PublishedRecord(
        ExecutionId execution,
        NodePath plan,
        PortName port) =>
        new(
            plan,
            port,
            1,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero),
            true,
            [
                new PublicationValueRecord(
                    new ContextSourceRecord(ContextSourceKind.Execution, execution, plan, port),
                    "计划结果"),
            ]);

    [Fact]
    public void Restore_rejects_active_publication_without_port_history()
    {
        TaskRecord record = DoneRecord with { Ports = [DoneRecord.Ports[0] with { Revision = 2 }] };
        ErrorOr<WorkTask> restored = CreateRestorer().Restore(record, CompileFixtureFlow());

        Assert.True(restored.IsError);
    }

    [Fact]
    public void Restore_preserves_published_content()
    {
        ErrorOr<WorkTask> restored = CreateRestorer().Restore(DoneRecord, CompileFixtureFlow());
        Assert.False(restored.IsError);
        WorkTask task = restored.Value;
        NodePath plan = NodePath.Of("整体", "制定计划");
        PortName port = new("计划");

        Assert.Equal("计划结果", task.Context.Runtime(plan).PortText(port));
    }

    private sealed class RecordingRunner : IFlowRunner
    {
        public int RestoreCount { get; private set; }

        public IReadOnlyList<NodePath> Seed { get; private set; } = [];

        public void Start(WorkTask task) => throw new NotSupportedException();

        public void Restore(WorkTask task, IReadOnlyList<NodePath> seed)
        {
            RestoreCount++;
            Seed = seed;
        }

        public void Pause(WorkTask task) => throw new NotSupportedException();

        public ErrorOr<Success> StopExecution(WorkTask task, Execution execution) =>
            throw new NotSupportedException();

        public void ResumeTask(WorkTask task, bool continueRestores = true) => throw new NotSupportedException();

        public void Advance(WorkTask task, IReadOnlyList<NodePath> nodes) => throw new NotSupportedException();

        public int Rerun(WorkTask task) => throw new NotSupportedException();

        public ErrorOr<Success> Terminate(WorkTask task) => throw new NotSupportedException();

        public Task ShutdownAsync() => Task.CompletedTask;
    }

    private sealed class AcceptAllRestorer : IExecutionRestorerSource
    {
        public ErrorOr<PreparedRestore> Prepare(
            ExecutableNode node,
            RestorePreparationContext context) =>
            new PreparedRestore(context.Input, new TestData());

        public IExecutionRunner CreateRestored(
            ExecutableNode node,
            ExecutionId execution,
            PreparedRestore restore) =>
            throw new NotSupportedException();
    }

    private sealed record TestData : IExecutionRestoreData;

    public void Dispose() => _environment.Dispose();
}
