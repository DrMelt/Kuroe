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
            new MafStep("第一步", "第一步", [new PortName("输出")], _ => new FakeTarget("第一步")),
            new MafStep("第二步", "第二步", [new PortName("输出")], _ => new FakeTarget("第二步")),
        ],
        [new MafEdge("第一步", "第二步")]);

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
        [new MafStep("唯一", "唯一", [new PortName("输出")], _ => new FakeTarget("唯一"))],
        []);

    internal static MafFlowDefinition Fork() => Definition(
        [
            new MafStep("起点", "起点", [new PortName("输出")], _ => new FakeTarget("起点")),
            new MafStep("左", "左", [new PortName("输出")], _ => new FakeTarget("左")),
            new MafStep("右", "右", [new PortName("输出")], _ => new FakeTarget("右")),
        ],
        [new MafEdge("起点", "左"), new MafEdge("起点", "右")]);

    internal static StartInputs Inputs() => new(
    [
        new StartInput(new PortName("目标"), "做点事"),
        new StartInput(MafFlowDefinition.ModelPort, "test-model"),
    ]);
}

/// <summary>执行目标：标识与步骤标识一致。</summary>
internal sealed class FakeTarget(string id) : IExecutionTarget
{
    public ExecutionTargetId Id => new(id);

    public string Kind => FakeRunnerFactory.KindName;

    public bool UsesDefaultRunner => false;

    public IExecutionParameters Parameters => EmptyExecutionParameters.Instance;
}

/// <summary>按种类建立测试运行器，保留建立的运行器供测试驱动。</summary>
internal sealed class FakeRunnerFactory(Func<FakeRunner> create) : IExecutionRunnerFactory
{
    internal const string KindName = "test";
    private readonly List<FakeRunner> _runners = [];

    public string Kind => KindName;

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
internal sealed class UnusedGateway : IFlowExecutionGateway
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
        IExecutionRunnerFactory? own,
        IExecutionTarget target) => throw new NotSupportedException();

    public Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared) => throw new NotSupportedException();

    public void Interrupt(IExecution execution, string reason) => throw new NotSupportedException();

    public void Reject(IExecution execution, string reason) => throw new NotSupportedException();
}