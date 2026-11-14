using ErrorOr;
using Kuroe.Execution.Shared;

namespace Kuroe.Control.Shared;

/// <summary>按执行号查找执行与归属任务的中性索引。契约工具只依赖它。</summary>
public interface IExecutionOwnerIndex
{
    /// <summary>按执行号取执行与它的归属任务，不存在时返回错误。</summary>
    ErrorOr<(IExecution Execution, ITaskFlowContext Owner)> FindOwned(ExecutionId id);
}