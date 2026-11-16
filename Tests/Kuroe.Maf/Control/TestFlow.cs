using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Tasks.Shared;

namespace Kuroe.Maf.Control.Tests;

/// <summary>MAF 流程测试使用的定义、开始输入与可控执行器。</summary>
internal static class TestFlow
{
    internal static FlowName Name { get; } = new("测试");

    internal static MafFlowDefinition Definition() => Definition(
        [
            Step("第一步"),
            Step("第二步"),
        ],
        [Edge("第一步", "第二步")]);

    internal static MafFlowDefinition Definition(IReadOnlyList<MafStep> steps, IReadOnlyList<MafEdge> edges) =>
        new(
            Name,
            [
                new MafStartPort(new PortName("目标"), "默认目标"),
                new MafStartPort(MafFlowDefinition.ModelPort),
            ],
            steps,
            edges)
        {
            Description = "顺序编排",
        };

    internal static MafFlowDefinition SingleStep() => Definition(
        [Step("唯一")],
        []);

    internal static MafFlowDefinition Fork() => Definition(
        [
            Step("起点"),
            Step("左"),
            Step("右"),
        ],
        [Edge("起点", "左"), Edge("起点", "右")]);

    internal static MafStep Step(string id) =>
        new(new MafStepId(id), id, [new PortName("输出")], Target());

    internal static MafEdge Edge(string from, string to) =>
        new(new MafStepId(from), new MafStepId(to));

    internal static MafStepTarget Target() =>
        new(FakeRunnerFactory.KindName, _ => EmptyExecutionParameters.Instance);

    internal static StartInputs Inputs() => new(
    [
        new StartInput(new PortName("目标"), "做点事"),
        new StartInput(MafFlowDefinition.ModelPort, "test-model"),
    ]);
}

/// <summary>按种类建立测试运行器，保留建立的运行器供测试驱动。</summary>
internal sealed class FakeRunnerFactory(Func<FakeRunner> create) : IExecutionRunnerFactory
{
    internal static ExecutionKind KindName { get; } = new("test");
    private readonly List<FakeRunner> _runners = [];

    public ExecutionKind Kind => KindName;

    internal IReadOnlyList<FakeRunner> Runners
    {
        get
        {
            lock (_runners)
            {
                return [.. _runners];
            }
        }
    }

    public IExecutionRunner Create(IExecutionTarget target, ExecutionId execution)
    {
        FakeRunner runner = create();
        lock (_runners)
        {
            _runners.Add(runner);
        }

        return runner;
    }
}

/// <summary>可控的测试运行器：可阻塞、可失败，成功时交回空端口。</summary>
internal sealed class FakeRunner : IExecutionRunner, IProcessJournal
{
    internal bool Block { get; set; }

    internal Error? Failure { get; set; }

    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IProcessJournal Journal => this;

    public IReadOnlyList<JournalEntry> Entries => [];

    public event ProcessContentHandler? ContentAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event JournalEntryHandler? EntryAppended
    {
        add => _ = value;
        remove => _ = value;
    }

    public event ProgressTextHandler? ProgressReported
    {
        add => _ = value;
        remove => _ = value;
    }

    public async Task<ErrorOr<ExecutionOutput>> ExecuteAsync(
        ExecutionRequest request,
        CancellationToken cancellationToken)
    {
        Started.TrySetResult();
        if (Block)
        {
            await Release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Failure is { } failure ? failure : ExecutionOutput.FromPorts([]);
    }
}

/// <summary>只用于提交与装配测试的执行网关，任何调用都表示测试越界。</summary>
internal sealed class UnusedGateway : ITaskExecutionGateway
{
    public IExecution Open(ITaskFlowContext owner, ExecutionRequest request) =>
        throw new NotSupportedException();

    public ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id) =>
        throw new NotSupportedException();

    public IExecution Restore(ExecutionRestorePlan plan) => throw new NotSupportedException();

    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        throw new NotSupportedException();

    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        IExecutionTarget target) => throw new NotSupportedException();

    public Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared) => throw new NotSupportedException();

    public void Interrupt(IExecution execution, string reason) => throw new NotSupportedException();

    public void Reject(IExecution execution, string reason) => throw new NotSupportedException();
}
