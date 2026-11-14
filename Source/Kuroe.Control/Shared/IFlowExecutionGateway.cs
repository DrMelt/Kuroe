using ErrorOr;
using Kuroe.Execution.Shared;

namespace Kuroe.Control.Shared;

/// <summary>流程控制访问执行单元的唯一端口：建立、查找、恢复、运行、中断与拒绝执行。</summary>
public interface IFlowExecutionGateway : IExecutionOwnerIndex
{
    /// <summary>为任务建立一个排队中的执行并分配执行号。</summary>
    IExecution Open(ITaskFlowContext owner, ExecutionRequest request);

    /// <summary>按存档数据重建一次已收口的执行。</summary>
    IExecution Restore(ExecutionRestorePlan plan);

    /// <summary>校验恢复负载并准备一次恢复。</summary>
    ErrorOr<PreparedExecutionRestore> PrepareRestore(ExecutionRestoreContext context);

    /// <summary>选择运行器、派发执行并等待收口。</summary>
    Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        IExecutionRunnerFactory? own,
        IExecutionTarget target);

    /// <summary>选择恢复运行器、记录来源执行、派发恢复执行并等待收口。</summary>
    Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared);

    /// <summary>请求中断执行。</summary>
    void Interrupt(IExecution execution, string reason);

    /// <summary>把已完成但未交回要求的执行降级为失败。</summary>
    void Reject(IExecution execution, string reason);
}