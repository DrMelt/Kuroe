using ErrorOr;
using Kuroe.Execution.Shared.Identity;
using Kuroe.Execution.Shared.Request;
using Kuroe.Execution.Shared.Restore;
using Kuroe.Execution.Shared.State;

namespace Kuroe.Execution.Shared.Runtime;

/// <summary>执行单元的控制面：建立、恢复、运行收口、中断、拒绝未收口执行与退出收口。执行状态机只在执行单元内部推进。</summary>
public interface IExecutionRuntime
{
    /// <summary>按执行号建立一个排队中的执行。</summary>
    IExecution Create(ExecutionId id, ExecutionRequest request);

    /// <summary>按存档数据重建一次已收口的执行。</summary>
    IExecution Restore(ExecutionRestorePlan plan);

    /// <summary>按装配结果建立运行器、派发执行并等待收口。派发异常把执行收为失败并返回错误。</summary>
    Task<ErrorOr<Success>> RunAsync(
        IExecution execution,
        ExecutionInvocation invocation);

    /// <summary>选择恢复运行器、记录来源执行、派发恢复执行并等待收口。派发异常把执行收为失败并返回错误。</summary>
    Task<ErrorOr<Success>> RunRestoreAsync(
        IExecution execution,
        IExecution source,
        PreparedExecutionRestore prepared);

    /// <summary>请求中断执行。排队与在跑的执行都按执行状态收口。</summary>
    void Interrupt(IExecution execution, string reason);

    /// <summary>把未开始或未交回要求的执行降级为失败。</summary>
    void Reject(IExecution execution, string reason);

    /// <summary>中断排队与在跑的执行并等待收口。</summary>
    Task ShutdownAsync();
}