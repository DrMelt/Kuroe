using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.Runtime;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Control.Runtime;

/// <summary>执行网关：把执行号归属交给任务域，装配执行目标，其余执行操作交给执行单元。</summary>
public sealed class TaskExecutionGateway(
    IExecutionIndex executions,
    IExecutionRuntime runtime,
    IExecutionInvocationFactory invocations) : ITaskExecutionGateway
{
    /// <inheritdoc/>
    public IExecution Open(ITaskFlowContext owner, ExecutionRequest request) =>
        executions.Open(owner, request, runtime.Create);

    /// <inheritdoc/>
    public ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id) =>
        executions.FindOwned(id);

    /// <inheritdoc/>
    public IExecution Restore(ExecutionRestorePlan plan) =>
        runtime.Restore(plan);

    /// <inheritdoc/>
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        invocations.PrepareRestore(context);

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        ExecutionTarget target)
    {
        ErrorOr<ExecutionInvocation> invocation = invocations.Create(target);
        if (invocation.IsError)
        {
            runtime.Reject(execution, invocation.FirstError.Description);

            return Task.FromResult<ErrorOr<Success>>(invocation.ErrorsOrEmptyList);
        }

        return runtime.RunAsync(execution, invocation.Value);
    }

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared) =>
        runtime.RunRestoreAsync(execution, source, prepared);

    /// <inheritdoc/>
    public void Interrupt(IExecution execution, string reason) =>
        runtime.Interrupt(execution, reason);

    /// <inheritdoc/>
    public void Reject(IExecution execution, string reason) =>
        runtime.Reject(execution, reason);
}