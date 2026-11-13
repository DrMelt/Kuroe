using ErrorOr;
using Kuroe.Flow.Execution;

namespace Kuroe.Flow.Control;

/// <summary>流程控制建立与登记执行的中性端口。</summary>
public interface IFlowExecutionGateway
{
    /// <summary>为任务建立一个排队中的执行并分配执行号。</summary>
    IExecution Open(
        ITaskFlowContext owner,
        ExecutionRequest request,
        Func<ExecutionId, ExecutionRequest, IExecution> create);

    /// <summary>按执行号取执行与它的归属任务，不存在时返回错误。</summary>
    ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id);
}