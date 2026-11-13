using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>执行单元的控制面：选择运行器、派发执行、等待收口与退出收口。执行状态迁移不出执行单元。</summary>
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
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        restorers.Prepare(context);

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        IExecutionRunnerFactory? own,
        IExecutionTarget target)
    {
        try
        {
            IExecutionRunner runner = runners.Create(own, target, execution.Id);

            return DispatchAsync(execution, runner);
        }
        catch (Exception ex)
        {
            return Task.FromResult(DispatchFailure(execution, ex));
        }
    }

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared)
    {
        try
        {
            Unit(execution).ContinueFrom(Unit(source));
            IExecutionRunner runner = restorers.CreateRestored(execution.Id, prepared);

            return DispatchAsync(execution, runner);
        }
        catch (Exception ex)
        {
            return Task.FromResult(DispatchFailure(execution, ex));
        }
    }

    /// <inheritdoc/>
    public void Interrupt(IExecution execution, string reason) => Unit(execution).Interrupt(reason);

    /// <inheritdoc/>
    public void Reject(IExecution execution, string reason) => Unit(execution).MarkUncollected(reason);

    /// <inheritdoc/>
    public Task ShutdownAsync() => dispatcher.ShutdownAsync();

    private async Task<ErrorOr<Success>> DispatchAsync(IExecution execution, IExecutionRunner runner)
    {
        try
        {
            await dispatcher.DispatchAsync(Unit(execution), runner, CancellationToken.None).ConfigureAwait(false);

            return Result.Success;
        }
        catch (Exception ex)
        {
            return DispatchFailure(execution, ex);
        }
    }

    private static ErrorOr<Success> DispatchFailure(IExecution execution, Exception exception)
    {
        string message = $"执行派发异常：{exception.Message}";
        Unit(execution).MarkFailed([Error.Failure("Execution.Dispatch", message)]);

        return Error.Failure("Execution.Dispatch", message);
    }

    private static Execution Unit(IExecution execution) => (Execution)execution;
}