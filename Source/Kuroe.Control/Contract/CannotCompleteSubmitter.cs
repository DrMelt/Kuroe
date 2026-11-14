using ErrorOr;
using Kuroe.Control.Shared;
using Kuroe.Execution.Shared;
using ExecutionStatus = Kuroe.Execution.Shared.ExecutionStatus;

namespace Kuroe.Control.Contract;

/// <summary>模型侧上报本轮无法完成的入口：给出原因即停驻待重新执行。</summary>
public sealed class CannotCompleteSubmitter(
    IExecutionOwnerIndex executions,
    IExecutionContractWriter contracts) : IExecutionCannotCompleteSubmitter
{
    /// <summary>上报本轮无法完成并给出原因，原因必填。</summary>
    public ErrorOr<ExecutionStatus> Report(ExecutionId id, string reason)
    {
        ErrorOr<(IExecution Execution, ITaskFlowContext Task)> owner = SubmitterContext.Resolve(
            executions,
            id,
            "上报无法完成");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        (IExecution execution, ITaskFlowContext task) = owner.Value;
        string recorded = reason.Trim();
        lock (task.Gate)
        {
            contracts.ReportCannotComplete(execution, recorded);

            return ExecutionStatusReader.Of(execution);
        }
    }
}