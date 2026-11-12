using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>执行层的统一入口：运行器选择、派发、恢复建立与收口等待。</summary>
public sealed class ExecutionRuntime(
    ExecutionDispatcher dispatcher,
    ExecutionRunnerRegistry runners,
    ExecutionRestorerRegistry restorers) : IExecutionRuntime
{
    /// <summary>为一次执行建立运行器。</summary>
    public IExecutionRunner CreateRunner(
        IExecutionRunnerFactory? own,
        IExecutionTarget target,
        ExecutionId execution) =>
        runners.Create(own, target, execution);

    /// <summary>入队一个执行并等它收口。</summary>
    public Task DispatchAsync(
        Execution execution,
        IExecutionRunner runner,
        CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(execution, runner, cancellationToken);

    /// <summary>校验恢复负载并准备一次恢复。</summary>
    public ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context) =>
        restorers.Prepare(context);

    /// <summary>按恢复结果建立运行器。</summary>
    public IExecutionRunner CreateRestored(
        ExecutionId execution,
        PreparedExecutionRestore prepared) =>
        restorers.CreateRestored(execution, prepared);

    /// <summary>中断排队与在跑的执行并等待收口。</summary>
    public Task ShutdownAsync() => dispatcher.ShutdownAsync();
}