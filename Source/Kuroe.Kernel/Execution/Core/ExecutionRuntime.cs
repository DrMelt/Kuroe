using ErrorOr;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Execution;

/// <summary>执行单元的控制面：按装配结果派发执行、等待收口与退出收口。执行状态迁移不出执行单元。</summary>
public sealed class ExecutionRuntime(
    ExecutionDispatcher dispatcher) : IExecutionRuntime
{
    /// <inheritdoc/>
    public IExecution Create(ExecutionId id, ExecutionRequest request) => ExecutionFactory.Create(id, request);

    /// <inheritdoc/>
    public IExecution Restore(ExecutionRestorePlan plan) => ExecutionFactory.Restore(plan);

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        ExecutionInvocation invocation)
    {
        try
        {
            if (invocation.Target.Id != execution.Request.Target)
            {
                throw new InvalidOperationException(
                    $"执行目标 {invocation.Target.Id} 与请求目标 {execution.Request.Target} 不一致。");
            }

            IExecutionRunner runner = invocation.Factory.Create(invocation.Target.Parameters, execution.Id);

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
            if (prepared.Context.Target.Id != prepared.Context.Request.Target
                || prepared.Invocation.Target.Id != prepared.Context.Target.Id)
            {
                throw new InvalidOperationException(
                    $"恢复目标 {prepared.Invocation.Target.Id} 与请求目标 {prepared.Context.Request.Target} 不一致。");
            }

            Unit(execution).ContinueFrom(Unit(source));
            IExecutionRestorer restorer = prepared.Invocation.Factory as IExecutionRestorer
                ?? throw new InvalidOperationException(
                    $"执行器工厂 {prepared.Invocation.Target.RunnerKey} 不支持恢复。");
            IExecutionRunner runner = restorer.CreateRestored(
                prepared.Context with { Execution = execution.Id },
                prepared.Data);

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

    private static ExecutionInstance Unit(IExecution execution) => (ExecutionInstance)execution;
}