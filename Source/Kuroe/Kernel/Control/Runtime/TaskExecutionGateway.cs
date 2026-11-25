using ErrorOr;
using Kuroe.Kernel.Control.Shared;
using Kuroe.Kernel.Execution.Shared.Identity;
using Kuroe.Kernel.Execution.Shared.Request;
using Kuroe.Kernel.Execution.Shared.Restore;
using Kuroe.Kernel.Execution.Shared.Runtime;
using Kuroe.Kernel.Execution.Shared.State;

namespace Kuroe.Kernel.Control.Runtime;

/// <summary>执行网关：把执行号归属交给任务域，装配执行，其余执行操作交给执行核心。</summary>
public sealed class TaskExecutionGateway(
    IExecutionIndex executions,
    IExecutionRuntime runtime) : ITaskExecutionGateway
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
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context)
    {
        if (context.Invocation.Step != context.Request.Step)
        {
            return Error.Failure(
                "Execution.Assembly",
                $"执行步骤 {context.Invocation.Step} 与请求步骤 {context.Request.Step} 不一致。");
        }

        if (context.Invocation.Factory is not IExecutionRestorer restorer)
        {
            return Error.Failure("Execution.Assembly", "执行器工厂不支持恢复。");
        }

        ErrorOr<IExecutionRestoreData> data = restorer.Prepare(context);

        return data.IsError
            ? data.ErrorsOrEmptyList
            : new PreparedExecutionRestore(context.Invocation, context, data.Value);
    }

    /// <inheritdoc/>
    public Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        ExecutionInvocation invocation) =>
        runtime.RunAsync(execution, invocation);

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
