using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Execution.Shared;
using Kuroe.Execution;
using Kuroe.Flow.Tasks.Shared;
using Xunit;

namespace Kuroe.Flow.Control.Contract.Tests;

/// <summary>中性契约工具：以执行请求与执行记录完成提交、上报与状态读取，不依赖具体流程控制。</summary>
public sealed class ContractToolsTests
{
    private static readonly ExecutionPortName Conclusion = new("结论");
    private static readonly ExecutionContractWriter Writer = new();

    [Fact]
    public void Submit_records_outputs_and_reports_no_outstanding()
    {
        ExecutionRuntime runtime = Runtime();
        FakeGateway gateway = new();
        FakeTask task = new();
        IExecution execution = Create(runtime, gateway, task, new ExecutionId(1), Conclusion);
        PortSubmitter submitter = new(gateway, Writer);

        ErrorOr<ExecutionStatus> status = submitter.Submit(
            execution.Id,
            [new ExecutionOutputValue(Conclusion, "完成")]);

        Assert.False(status.IsError);
        Assert.Empty(status.Value.OutstandingOutputs);
        Assert.Equal(3, status.Value.MaxAttempts);
        Assert.Equal("完成", execution.Contract.Outputs![Conclusion].Text);
    }

    [Fact]
    public void Submit_rejects_a_second_submission()
    {
        ExecutionRuntime runtime = Runtime();
        FakeGateway gateway = new();
        FakeTask task = new();
        IExecution execution = Create(runtime, gateway, task, new ExecutionId(1), Conclusion);
        PortSubmitter submitter = new(gateway, Writer);

        submitter.Submit(execution.Id, [new ExecutionOutputValue(Conclusion, "完成")]).ThrowIfError();
        ErrorOr<ExecutionStatus> second = submitter.Submit(
            execution.Id,
            [new ExecutionOutputValue(Conclusion, "再次")]);

        Assert.Equal("端口产出已提交，无需重复提交。", second.FirstError.Description);
    }

    [Fact]
    public void Submit_rejects_blank_text()
    {
        ExecutionRuntime runtime = Runtime();
        FakeGateway gateway = new();
        FakeTask task = new();
        IExecution execution = Create(runtime, gateway, task, new ExecutionId(1), Conclusion);
        PortSubmitter submitter = new(gateway, Writer);

        ErrorOr<ExecutionStatus> status = submitter.Submit(
            execution.Id,
            [new ExecutionOutputValue(Conclusion, " ")]);

        Assert.Contains("端口 结论 的产出为空", status.FirstError.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Report_records_the_reason()
    {
        ExecutionRuntime runtime = Runtime();
        FakeGateway gateway = new();
        FakeTask task = new();
        IExecution execution = Create(runtime, gateway, task, new ExecutionId(1), Conclusion);
        CannotCompleteSubmitter submitter = new(gateway, Writer);

        ErrorOr<ExecutionStatus> status = submitter.Report(execution.Id, " 缺少资料 ");

        Assert.False(status.IsError);
        Assert.Equal("缺少资料", status.Value.CannotCompleteReason);
        Assert.Equal(["结论"], status.Value.OutstandingOutputs.Select(port => port.Value));
    }

    [Fact]
    public void Status_reads_the_live_execution_and_rejects_an_unknown_id()
    {
        ExecutionRuntime runtime = Runtime();
        FakeGateway gateway = new();
        FakeTask task = new();
        IExecution execution = Create(runtime, gateway, task, new ExecutionId(1), Conclusion);
        ExecutionStatusReader reader = new(gateway);

        ExecutionStatus status = reader.Read(execution.Id).ThrowIfError();

        Assert.Equal(new ExecutionTargetId("步骤"), status.Target);
        Assert.Equal([Conclusion], status.OutstandingOutputs);
        Assert.Equal("没有执行 执行 #99。", reader.Read(new ExecutionId(99)).FirstError.Description);
    }

    private static ExecutionRuntime Runtime() =>
        new(
            new ExecutionDispatcher(),
            new ExecutionRunnerRegistry(),
            new ExecutionRestorerRegistry());

    private static IExecution Create(
        ExecutionRuntime runtime,
        FakeGateway gateway,
        FakeTask task,
        ExecutionId id,
        ExecutionPortName port)
    {
        ExecutionRequest request = new(
            new ExecutionOwnerId(task.Id.Value),
            new ExecutionTargetId("步骤"),
            "test",
            1,
            [],
            [],
            new Dictionary<ExecutionPortName, string>(),
            [new ExecutionOutputPort(port, ExecutionPortKind.Text, true, false)],
            MaxAttempts: 3);
        IExecution execution = runtime.Create(id, request);
        gateway.Register(execution, task);

        return execution;
    }
}
internal sealed class FakeGateway : IFlowExecutionGateway
{
    private readonly Dictionary<ExecutionId, (IExecution Execution, ITaskFlowContext Owner)> _entries = [];

    public IExecution Open(
        ITaskFlowContext owner,
        ExecutionRequest request) =>
        throw new NotSupportedException();

    public IExecution Restore(ExecutionRestorePlan plan) => throw new NotSupportedException();

    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        throw new NotSupportedException();

    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        IExecutionRunnerFactory? own,
        IExecutionTarget target) =>
        throw new NotSupportedException();

    public Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared) =>
        throw new NotSupportedException();

    public void Interrupt(IExecution execution, string reason) => throw new NotSupportedException();

    public void Reject(IExecution execution, string reason) => throw new NotSupportedException();

    public ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id) =>
        _entries.TryGetValue(id, out (IExecution Execution, ITaskFlowContext Owner) entry)
            ? entry
            : [ExecutionErrors.ExecutionNotFound(id.Value)];

    public void Register(IExecution execution, ITaskFlowContext owner) =>
        _entries[execution.Id] = (execution, owner);
}

internal sealed class FakeTask : ITaskFlowContext
{
    public Lock Gate { get; } = new();

    public TaskId Id => new(7);

    public DateTimeOffset CreatedAt => DateTimeOffset.UnixEpoch;

    public DateTimeOffset LastActivityAt => DateTimeOffset.UnixEpoch;

    public StartInputs Inputs => new([]);

    public IFlowState FlowState => throw new NotSupportedException();

    public string Title => "测试";

    public TaskState State => TaskState.Running;

    public TaskLifecycle Lifecycle => TaskLifecycle.Active;

    public IFlowControl? Control => null;

    public void AttachExecution(IExecution execution) => throw new NotSupportedException();

    public bool Completed => false;

    public int ControlVersion => 0;

    public IReadOnlyList<IExecution> Executions => [];

    public IReadOnlyList<JournalEntry> JournalEntries => [];

    public int DroppedJournal => 0;

    public void AppendInfo(string text) => throw new NotSupportedException();

    public void AppendError(string text) => throw new NotSupportedException();

    public void Report(ExecutionNotice notice) => throw new NotSupportedException();

    public bool PauseOnRestore => false;

    public void AppendJournal(JournalLevel level, string text) => throw new NotSupportedException();

    public void RestoreJournal(IReadOnlyList<JournalEntry> entries, int dropped) => throw new NotSupportedException();

    public void NotifyChanged() => throw new NotSupportedException();

    public void Touch() => throw new NotSupportedException();

    public void MarkCompleted() => throw new NotSupportedException();

    public ErrorOr<Success> Pause() => throw new NotSupportedException();

    public ErrorOr<Success> Resume() => throw new NotSupportedException();

    public ErrorOr<Success> Terminate() => throw new NotSupportedException();

    public void PauseForRestore() => throw new NotSupportedException();

    public void NotifyExecutionSettled(IExecution execution) => throw new NotSupportedException();

    public Action SubscribeExecutionSettled(Action<IExecution> handler) => throw new NotSupportedException();

    public Action SubscribeExecutionAttached(Action<IExecution> handler) => throw new NotSupportedException();

    public T UnderLock<T>(Func<T> action) => action();
}