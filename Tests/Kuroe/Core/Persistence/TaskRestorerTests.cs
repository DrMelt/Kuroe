using ErrorOr;
using Kuroe.Flow.Runtime;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Runtime.Tasks.Persistence;
using Kuroe.Flow.Shared.Model;
using Kuroe.Flow.Persistence.Shared;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using Kuroe.Flow.Shared.RuntimeGraph.Ports;
using Kuroe.Flow.Shared.RuntimeGraph.Tasks;
using Kuroe.Flow.Shared.StaticGraph.Flow;
using Kuroe.Flow.TestSupport;
using Xunit;

namespace Kuroe.Tests.Persistence;

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
            new FlowSnapshot(new FlowName("默认"), """{"Flows":[]}"""),
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
                    Common(plan, published ? 1 : 0, blocked),
                    1,
                    published ? execution : null,
                    published ? new Dictionary<PortName, PortValue> { [result] = new("计划结果") } : []),
                new ContainerNodeCheckpoint(container, Common(container, published ? 1 : 0, false)),
            ],
            new SchedulerSeed([]));
    }

    private static NodeCommonCheckpoint Common(NodePath path, long revision, bool blocked) =>
        new(
            revision,
            null,
            [new ConsumedRevisionCheckpoint(NodePath.Of("整体"), new PortName("目标"), 1)],
            blocked ? [path] : [],
            blocked,
            false);

    public void Dispose() => _environment.Dispose();
}
