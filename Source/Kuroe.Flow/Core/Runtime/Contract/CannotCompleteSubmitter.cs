using ErrorOr;
using Kuroe.Flow.Execution;
using Kuroe.Flow.Runtime.Tasks;
using Kuroe.Flow.Shared.RuntimeGraph.Execution;
using NeutralExecutionId = Kuroe.Flow.Execution.ExecutionId;
using NeutralExecutionStatus = Kuroe.Flow.Execution.ExecutionStatus;

namespace Kuroe.Flow.Runtime.Contract;

/// <summary>模型侧上报本轮无法完成的入口：给出原因即停驻待重新执行。
/// 提交者身份在此认定，结果以上报后的本轮契约状态交回，回执措辞由工具实现层给出。</summary>
public sealed class CannotCompleteSubmitter(ExecutionIndex executions) : IExecutionCannotCompleteSubmitter
{
    /// <summary>上报本轮无法完成并给出原因，原因必填。成功时给出上报后的本轮契约状态，错误以 ErrorOr 表达。</summary>
    public ErrorOr<NeutralExecutionStatus> Report(NeutralExecutionId id, string reason)
    {
        ErrorOr<(GraphExecution Execution, WorkTask Task)> owner = SubmitterContext.Resolve(
            executions,
            new Kuroe.Flow.Shared.RuntimeGraph.Execution.ExecutionId(id.Value),
            "上报无法完成");
        if (owner.IsError)
        {
            return owner.ErrorsOrEmptyList;
        }

        (GraphExecution execution, WorkTask task) = owner.Value;
        string recorded = reason.Trim();
        lock (task.Gate)
        {
            execution.ReportCannotComplete(recorded);

            return ExecutionStatusReader.Of(task, execution);
        }
    }
}