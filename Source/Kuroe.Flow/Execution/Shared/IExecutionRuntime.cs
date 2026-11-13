using ErrorOr;

namespace Kuroe.Flow.Execution;

/// <summary>执行层的统一入口：建立、恢复、派发、收口命令与运行器选择。</summary>
public interface IExecutionRuntime
{
    /// <summary>按执行号建立一个排队中的执行。</summary>
    IExecution Create(ExecutionId id, ExecutionRequest request);

    /// <summary>按存档数据重建一次已收口的执行。</summary>
    IExecution Restore(ExecutionRestorePlan plan);

    /// <summary>为一次执行建立运行器。</summary>
    IExecutionRunner CreateRunner(
        IExecutionRunnerFactory? own,
        IExecutionTarget target,
        ExecutionId execution);

    /// <summary>入队一个执行并等它收口。</summary>
    Task DispatchAsync(
        IExecution execution,
        IExecutionRunner runner,
        CancellationToken cancellationToken = default);

    /// <summary>校验恢复负载并准备一次恢复。</summary>
    ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context);

    /// <summary>按恢复结果建立运行器。</summary>
    IExecutionRunner CreateRestored(ExecutionId execution, PreparedExecutionRestore prepared);

    /// <summary>记下执行开始运行。</summary>
    void MarkRunning(IExecution execution);

    /// <summary>附加执行的过程记录。</summary>
    void AttachJournal(IExecution execution, IProcessJournal journal);

    /// <summary>附加执行的用量来源。</summary>
    void AttachUsageReader(IExecution execution, IExecutionUsageSource reader);

    /// <summary>记下执行的最终用量。</summary>
    void SettleUsage(IExecution execution, ExecutionUsage usage);

    /// <summary>附加执行的恢复数据读取面。</summary>
    void AttachRestoreDataReader(IExecution execution, IExecutionRestoreDataSource reader);

    /// <summary>记下执行的最终恢复数据。</summary>
    void SettleRestoreData(IExecution execution, IExecutionRestoreDataSource? reader);

    /// <summary>记下执行从哪次执行恢复。</summary>
    void ContinueFrom(IExecution execution, IExecution source);

    /// <summary>记下执行交回的端口值。</summary>
    void Submit(IExecution execution, IReadOnlyDictionary<ExecutionPortName, ExecutionOutputValue> outputs);

    /// <summary>记下执行上报的无法完成原因。</summary>
    void ReportCannotComplete(IExecution execution, string reason);

    /// <summary>把执行收为成功。</summary>
    void Succeed(IExecution execution, ExecutionOutput output);

    /// <summary>把执行收为失败。</summary>
    void Fail(IExecution execution, IReadOnlyList<Error> errors);

    /// <summary>请求中断执行。</summary>
    void Interrupt(IExecution execution, string reason);

    /// <summary>把执行直接记为已中断。</summary>
    void MarkInterrupted(IExecution execution, string? reason = null);

    /// <summary>把已完成但未交回要求的执行降级为失败。</summary>
    void Reject(IExecution execution, string reason);

    /// <summary>中断排队与在跑的执行并等待收口。</summary>
    Task ShutdownAsync();
}