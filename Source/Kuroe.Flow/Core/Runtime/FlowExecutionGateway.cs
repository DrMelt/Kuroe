using ErrorOr;
using Kuroe.Flow.Control.Shared;
using Kuroe.Execution.Shared;

namespace Kuroe.Flow.Runtime;

/// <summary>执行网关：把执行号归属交给任务域，其余执行操作交给执行单元。</summary>
public sealed class FlowExecutionGateway(
    ExecutionIndex executions,
    IExecutionRuntime runtime) : IFlowExecutionGateway
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
        runtime.PrepareRestore(context);

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        IExecutionRunnerFactory? own,
        IExecutionTarget target) =>
        runtime.RunAsync(execution, own, target);

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