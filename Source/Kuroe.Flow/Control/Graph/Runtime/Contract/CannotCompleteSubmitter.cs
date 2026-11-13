using ErrorOr;
using Kuroe.Flow.Control;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Runtime.Engine;
using ExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>模型侧上报本轮无法完成的入口：给出原因即停驻待重新执行。</summary>
public sealed class CannotCompleteSubmitter(IFlowExecutionGateway executions) : IExecutionCannotCompleteSubmitter
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
            GraphFlowControl control = task.Control as GraphFlowControl
                ?? throw new InvalidOperationException($"任务 {task.Id} 没有图流程控制。");

            return control.ReportCannotComplete(execution, recorded);
        }
    }
}