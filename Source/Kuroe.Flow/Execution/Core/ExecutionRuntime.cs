using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>执行层的统一入口：建立、恢复、派发、收口命令与运行器选择。</summary>
public sealed class ExecutionRuntime(
    ExecutionDispatcher dispatcher,
    ExecutionRunnerRegistry runners,
    ExecutionRestorerRegistry restorers) : IExecutionRuntime
{
    /// <inheritdoc/>
    public IExecution Create(ExecutionId id, ExecutionRequest request) => ExecutionFactory.Create(id, request);

    /// <inheritdoc/>
    public IExecution Restore(ExecutionRestorePlan plan) => ExecutionFactory.Restore(plan);

    /// <inheritdoc/>
    public IExecutionRunner CreateRunner(
        IExecutionRunnerFactory? own,
        IExecutionTarget target,
        ExecutionId execution) =>
        runners.Create(own, target, execution);

    /// <inheritdoc/>
    public Task DispatchAsync(
        IExecution execution,
        IExecutionRunner runner,
        CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(Unit(execution), runner, cancellationToken);

    /// <inheritdoc/>
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        restorers.Prepare(context);

    /// <inheritdoc/>
    public IExecutionRunner CreateRestored(
        ExecutionId execution,
        PreparedExecutionRestore prepared) =>
        restorers.CreateRestored(execution, prepared);

    /// <inheritdoc/>
    public void MarkRunning(IExecution execution) => Unit(execution).MarkRunning();

    /// <inheritdoc/>
    public void AttachJournal(IExecution execution, IProcessJournal journal) =>
        Unit(execution).AttachJournal(journal);

    /// <inheritdoc/>
    public void AttachUsageReader(IExecution execution, IExecutionUsageSource reader) =>
        Unit(execution).AttachUsageReader(reader);

    /// <inheritdoc/>
    public void SettleUsage(IExecution execution, ExecutionUsage usage) =>
        Unit(execution).SettleUsage(usage);

    /// <inheritdoc/>
    public void AttachRestoreDataReader(IExecution execution, IExecutionRestoreDataSource reader) =>
        Unit(execution).AttachRestoreDataReader(reader);

    /// <inheritdoc/>
    public void SettleRestoreData(IExecution execution, IExecutionRestoreDataSource? reader) =>
        Unit(execution).SettleRestoreData(reader);

    /// <inheritdoc/>
    public void ContinueFrom(IExecution execution, IExecution source) =>
        Unit(execution).ContinueFrom(source);

    /// <inheritdoc/>
    public void Submit(IExecution execution, IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs) =>
        Unit(execution).SubmitOutputs(outputs);

    /// <inheritdoc/>
    public void ReportCannotComplete(IExecution execution, string reason) =>
        Unit(execution).ReportCannotComplete(reason);

    /// <inheritdoc/>
    public void Succeed(IExecution execution, ExecutionOutput output) =>
        Unit(execution).MarkSucceeded(output);

    /// <inheritdoc/>
    public void Fail(IExecution execution, IReadOnlyList<Error> errors) =>
        Unit(execution).MarkFailed(errors);

    /// <inheritdoc/>
    public void Interrupt(IExecution execution, string reason) => Unit(execution).Interrupt(reason);

    /// <inheritdoc/>
    public void MarkInterrupted(IExecution execution, string? reason = null) =>
        Unit(execution).MarkInterrupted(reason);

    /// <inheritdoc/>
    public void Reject(IExecution execution, string reason) => Unit(execution).MarkUncollected(reason);

    /// <inheritdoc/>
    public Task ShutdownAsync() => dispatcher.ShutdownAsync();

    private static Execution Unit(IExecution execution) => (Execution)execution;
}