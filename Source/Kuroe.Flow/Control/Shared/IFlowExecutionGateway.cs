using ErrorOr;
using Kuroe.Flow.Execution;
using LoopExecution = Kuroe.Flow.Execution.Execution;

namespace Kuroe.Flow.Control;

/// <summary>流程控制建立与登记执行的中性端口。</summary>
public interface IFlowExecutionGateway
{
    /// <summary>为任务建立一个排队中的执行并分配执行号。</summary>
    LoopExecution Open(ITaskFlowContext owner, ExecutionRequest request);

    /// <summary>按执行号取执行，不存在时返回错误。</summary>
    ErrorOr<LoopExecution> Find(ExecutionId id);

    /// <summary>按执行号停止一个已启动的执行。</summary>
    ErrorOr<Success> Stop(ITaskFlowContext owner, ExecutionId id);
}