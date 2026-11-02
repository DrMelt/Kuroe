using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Integration.Tests.Persistence;

/// <summary>任务恢复契约的实现：用默认测试流程验证完成、未完成与中断三种检查点。</summary>
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

    protected override TaskCheckpoint DoneCheckpoint => Create(TaskLifecycle.Active, ExecutionState.Succeeded, published: true, blocked: false) with { Completed = true };

    protected override TaskCheckpoint UnfinishedCheckpoint => Create(TaskLifecycle.Paused, ExecutionState.Succeeded, published: false, blocked: false);

    protected override TaskCheckpoint InterruptedCheckpoint => Create(TaskLifecycle.Paused, ExecutionState.Interrupted, published: false, blocked: true);

    [Fact]
    public void Restore_registers_executions_in_shared_index()
    {
        TaskCheckpoint checkpoint = UnfinishedCheckpoint;

        ErrorOr<WorkTask> restored = CreateRestorer().Restore(checkpoint, CompileFixtureFlow());

        Assert.False(restored.IsError);
        Assert.False(_registry.FindExecution(checkpoint.Executions[0].Id).IsError);
    }

    private static TaskCheckpoint Create(
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

        return new TaskCheckpoint(
            new TaskIdentity(id, "恢复任务", at, at),
            new FlowSnapshot(new FlowName("默认"), """{"Nodes":[]}""", """{"Flows":[]}"""),
            new TaskStartSnapshot(
                StartInputs.Single(target, "目标内容"),
                new Dictionary<PortName, string> { [target] = "目标内容" }),
            lifecycle,
            Completed: false,
            new TaskJournalSnapshot([], 0),
            [
                new ExecutionCheckpoint(
                    execution,
                    plan,
                    1,
                    state,
                    string.Empty,
                    at,
                    at,
                    at,
                    state == ExecutionState.Interrupted ? ["任务已暂停。"] : [],
                    [],
                    0,
                    [new InputValueCheckpoint(target, new ContextSourceCheckpoint(ContextSourceKind.Start, null, null, target), "目标内容")],
                    new Dictionary<PortName, string> { [target] = "目标内容" }),
            ],
            [
                new ExecutableNodeCheckpoint(
                    plan,
                    Common(
                        published ? 1 : 0,
                        published ? PublishedCheckpoint(execution, plan, result) : null),
                    blocked ? [plan] : [],
                    blocked),
                new ContainerNodeCheckpoint(container, Common(published ? 1 : 0)),
            ],
            new SchedulerSeed([]));
    }

    private static NodeCommonCheckpoint Common(long revision, PublicationCheckpoint? publication = null) =>
        new(
            revision,
            1,
            null,
            [new ConsumedRevisionCheckpoint(NodePath.Of("整体"), new PortName("目标"), 1)],
            publication,
            false);

    private static PublicationCheckpoint PublishedCheckpoint(
        ExecutionId execution,
        NodePath plan,
        PortName port) =>
        new(
            new Dictionary<PortName, bool>(),
            new Dictionary<PortName, IReadOnlyList<PublicationValueCheckpoint>>
            {
                [port] =
                [
                    new PublicationValueCheckpoint(
                        new ContextSourceCheckpoint(ContextSourceKind.Execution, execution, plan, port),
                        "计划结果"),
                ],
            });

    [Fact]
    public void Restore_preserves_published_content()
    {
        ErrorOr<WorkTask> restored = CreateRestorer().Restore(DoneCheckpoint, CompileFixtureFlow());
        Assert.False(restored.IsError);
        WorkTask task = restored.Value;
        NodePath plan = NodePath.Of("整体", "制定计划");
        PortName port = new("计划");

        Assert.Equal("计划结果", task.Context.Runtime(plan).PortText(port));
    }

    public void Dispose() => _environment.Dispose();
}
