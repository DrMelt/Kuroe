using ErrorOr;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Execution;

/// <summary>执行核心的控制面：按装配结果派发执行、等待收口与退出收口。执行状态迁移不出执行核心。</summary>
public sealed class ExecutionRuntime(
    ExecutionDispatcher dispatcher,
    TimeProvider time) : IExecutionRuntime
{
    /// <inheritdoc/>
    public IExecution Create(ExecutionId id, ExecutionRequest request) => ExecutionFactory.Create(id, request, time);

    /// <inheritdoc/>
    public IExecution Restore(ExecutionRestorePlan plan) => ExecutionFactory.Restore(plan, time);

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        ExecutionInvocation invocation)
    {
        try
        {
            if (invocation.Step != execution.Request.Step)
            {
                throw new InvalidOperationException(
                    $"执行步骤 {invocation.Step} 与请求步骤 {execution.Request.Step} 不一致。");
            }

            ExecutionRun run = ExecutionRun.Create(Unit(execution), invocation);

            return DispatchAsync(execution, run);
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
            if (prepared.Context.Invocation.Step != prepared.Context.Request.Step
                || prepared.Invocation.Step != prepared.Context.Invocation.Step)
            {
                throw new InvalidOperationException(
                    $"恢复步骤 {prepared.Invocation.Step} 与请求步骤 {prepared.Context.Request.Step} 不一致。");
            }

            Unit(execution).ContinueFrom(Unit(source));
            ExecutionRun run = ExecutionRun.Restore(
                Unit(execution),
                prepared.Invocation,
                prepared.Context with { Execution = execution.Id },
                prepared.Data);

            return DispatchAsync(execution, run);
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

    private async Task<ErrorOr<Success>> DispatchAsync(IExecution execution, ExecutionRun run)
    {
        try
        {
            await dispatcher.DispatchAsync(run, CancellationToken.None).ConfigureAwait(false);

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
